"""Import public exchange labels only; never acquires Databento data or changes contract identities."""
import argparse
import csv
import hashlib
import io
import json
import pathlib
import subprocess
import urllib.request
import xml.etree.ElementTree as ET
import zipfile
from collections import defaultdict

CME_URL = 'https://www.cmegroup.com/tools-information/files/quote-vendor-codes.xlsx'
CFE_URL = 'https://cdn.cboe.com/data/us/futures/market_statistics/symbol_reference/latest-simple.csv'
NS = {'s': 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}


def cme_names(data):
    """Read named columns across every worksheet; preserve conflicting names for rejection."""
    names = defaultdict(set)
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        strings = [''.join(x.itertext()) for x in ET.fromstring(archive.read('xl/sharedStrings.xml')).findall('s:si', NS)]
        relations = {x.attrib['Id']: x.attrib['Target'] for x in ET.fromstring(archive.read('xl/_rels/workbook.xml.rels'))}
        for sheet in ET.fromstring(archive.read('xl/workbook.xml')).find('s:sheets', NS):
            target = relations[sheet.attrib['{http://schemas.openxmlformats.org/officeDocument/2006/relationships}id']]
            target = target.lstrip('/') if target.startswith('/') else 'xl/' + target
            columns = None
            for row in ET.fromstring(archive.read(target)).findall('.//s:sheetData/s:row', NS):
                cells = {}
                for cell in row:
                    value = cell.find('s:v', NS)
                    value = value.text if value is not None else ''
                    if cell.attrib.get('t') == 's':
                        value = strings[int(value)] if value else ''
                    elif cell.attrib.get('t') == 'inlineStr':
                        value = ''.join(cell.find('s:is', NS).itertext())
                    cells[''.join(x for x in cell.attrib['r'] if x.isalpha())] = str(value).strip()
                if 'Globex' in cells.values() and 'CME Group Product Name' in cells.values():
                    columns = {value: key for key, value in cells.items()}
                    continue
                if columns is None:
                    continue
                name = cells.get(columns['CME Group Product Name'], '')
                family = cells.get(columns['Futures/Options'], '')
                code = cells.get(columns['Globex'], '')
                if name and code and code != '--' and family in ('Futures', 'Options'):
                    names[(code, 1 if family == 'Futures' else 2)].add(name)
    if not names:
        raise ValueError('CME workbook contained no recognized product rows; previous labels retained')
    return names


def cfe_contracts(data):
    """Use the exchange description verbatim and exclude explicit test contracts."""
    rows = list(csv.DictReader(io.StringIO(data.decode('utf-8-sig'))))
    required = {'Futures Root', 'Product', 'Description', 'Test Symbol'}
    if not rows or not required.issubset(rows[0]):
        raise ValueError('CFE CSV schema changed; previous labels retained')
    return [row for row in rows if row['Test Symbol'].strip().lower() == 'false' and row['Description'].strip()]


def quote(value):
    """Quote data for psql input; never interpolate it into shell commands."""
    return "'" + str(value).replace("'", "''") + "'"


def psql(container, database, sql):
    """Use the container's existing credentials without printing them."""
    result = subprocess.run(['docker', 'exec', '-i', container, 'sh', '-c',
        'exec psql -X -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$1" -At', 'psql', database],
        input=sql, text=True, encoding="utf-8", capture_output=True)
    if result.returncode:
        raise RuntimeError(result.stderr)
    return result.stdout


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-directory', type=pathlib.Path, required=True)
    parser.add_argument('--container', default='ifm_db')
    parser.add_argument('--database', default='ifm_application_development')
    parser.add_argument('--download', action='store_true')
    parser.add_argument('--apply', action='store_true')
    args = parser.parse_args()
    args.source_directory.mkdir(parents=True, exist_ok=True)
    sources = [('quote-vendor-codes.xlsx', CME_URL), ('cfe-latest-simple.csv', CFE_URL)]
    if args.download:
        for filename, url in sources:
            request = urllib.request.Request(url, headers={'User-Agent': 'Mozilla/5.0'})
            with urllib.request.urlopen(request, timeout=30) as response:
                data = response.read(20_000_001)
            if len(data) > 20_000_000:
                raise ValueError('Exchange file exceeded the download bound')
            (args.source_directory / filename).write_bytes(data)
    cme = (args.source_directory / sources[0][0]).read_bytes()
    cfe = (args.source_directory / sources[1][0]).read_bytes()
    names = cme_names(cme)
    contracts = cfe_contracts(cfe)
    products = psql(args.container, args.database, """
        SET statement_timeout='20s';
        SELECT DISTINCT p.exchange,p.family,p.symbol FROM reference.instrument_definition_product p
        JOIN reference.instrument_definition_snapshot s ON s.snapshot_id=p.snapshot_id AND s.catalog='current'
        ORDER BY 1,2,3;
        """)
    labels = []
    stats = defaultdict(int)
    for line in products.splitlines():
        parts = line.split('|')
        if len(parts) != 3 or parts[0] not in ('XCME', 'XCBT', 'XNYM', 'XCEC'):
            continue
        exchange, family, symbol = parts
        options = names.get((symbol, int(family)), set())
        resolved = len(options) == 1
        description = next(iter(options)) if resolved else ''
        labels.append(('CME.QuoteVendorCodes', exchange, family, symbol, '', description, resolved, CME_URL, hashlib.sha256(cme).hexdigest()))
        stats[f'CME.{family}.' + ('resolved' if resolved else 'unresolved')] += 1
    for row in contracts:
        labels.append(('CFE.ContractListing', 'XCBF', 1, row['Futures Root'], row['Product'], row['Description'], True, CFE_URL, hashlib.sha256(cfe).hexdigest()))
    stats['CFE.contract_descriptions'] = len(contracts)
    if args.apply:
        repository = pathlib.Path(__file__).resolve().parents[2]
        schema = (repository / 'TomasAI.IFM.Application.Storage/ReferenceDb/Schema/ReferenceSchemaSql.cs').read_text(encoding='utf-8-sig')
        start = schema.index('CREATE TABLE IF NOT EXISTS reference.product_description')
        ddl = schema[start:schema.index(');', start) + 2]
        sql = ['BEGIN;', "SET LOCAL lock_timeout='10s';", "SET LOCAL statement_timeout='60s';", "SELECT pg_advisory_xact_lock(hashtextextended('ExchangeProductDescriptions',0));", ddl]
        for row in labels:
            values = ','.join('true' if v is True else 'false' if v is False else quote(v) for v in row)
            sql.append('INSERT INTO reference.product_description(source,exchange,family,symbol,contract,description,resolved,source_uri,source_sha256) VALUES(' + values + ') ON CONFLICT(source,exchange,family,symbol,contract) DO UPDATE SET description=EXCLUDED.description,resolved=EXCLUDED.resolved,source_uri=EXCLUDED.source_uri,source_sha256=EXCLUDED.source_sha256,imported_utc=now();')
        # Expired CFE contracts remain useful historical labels; none become tradable through this import.
        sql.append('COMMIT;')
        psql(args.container, args.database, '\n'.join(sql))
    report = {'applied': args.apply, 'statistics': dict(stats), 'sources': {url: hashlib.sha256(data).hexdigest() for (_, url), data in zip(sources, [cme, cfe])}}
    (args.source_directory / 'import-report.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
