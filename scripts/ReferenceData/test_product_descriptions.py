"""Pure source-parser checks; no network, credentials, database, or trading operations."""
import importlib.util
import io
import unittest
import zipfile
from pathlib import Path

spec = importlib.util.spec_from_file_location('labels', Path(__file__).with_name('import_product_descriptions.py'))
labels = importlib.util.module_from_spec(spec)
spec.loader.exec_module(labels)


class ProductDescriptionTests(unittest.TestCase):
    def test_named_columns_multiple_sheets_and_conflicting_names(self):
        strings = ['CME Group Product Name', 'Futures/Options', 'Globex', 'E-mini S&P 500 Futures', 'Futures', 'ES', 'Conflicting name', 'Options', 'E-mini S&P 500 Options']
        import xml.sax.saxutils
        shared = '<sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">' + ''.join('<si><t>' + xml.sax.saxutils.escape(s) + '</t></si>' for s in strings) + '</sst>'
        def row(number, indices):
            return '<row>' + ''.join(f'<c r="{column}{number}" t="s"><v>{index}</v></c>' for column,index in zip('ABC',indices)) + '</row>'
        def sheet(rows):
            return '<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>' + ''.join(rows) + '</sheetData></worksheet>'
        data = io.BytesIO()
        with zipfile.ZipFile(data,'w') as z:
            z.writestr('xl/sharedStrings.xml',shared)
            z.writestr('xl/workbook.xml','<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="One" r:id="one"/><sheet name="Two" r:id="two"/></sheets></workbook>')
            z.writestr('xl/_rels/workbook.xml.rels','<Relationships><Relationship Id="one" Target="worksheets/sheet1.xml"/><Relationship Id="two" Target="worksheets/sheet2.xml"/></Relationships>')
            z.writestr('xl/worksheets/sheet1.xml',sheet([row(1,[0,1,2]),row(2,[3,4,5]),row(3,[8,7,5])]))
            z.writestr('xl/worksheets/sheet2.xml',sheet([row(1,[0,1,2]),row(2,[6,4,5])]))
        result=labels.cme_names(data.getvalue())
        self.assertEqual({'E-mini S&P 500 Futures','Conflicting name'},result[('ES',1)])
        self.assertEqual({'E-mini S&P 500 Options'},result[('ES',2)])

    def test_cfe_descriptions_are_verbatim_and_tests_excluded(self):
        data=b'Futures Root,Product,Description,Test Symbol\nVX,VX/X6,VX Monthly Nov 2026,False\nZVX,ZVX/X6,Test,True\n'
        self.assertEqual('VX Monthly Nov 2026',labels.cfe_contracts(data)[0]['Description'])
        self.assertEqual(1,len(labels.cfe_contracts(data)))
        with self.assertRaises(ValueError): labels.cfe_contracts(b'Product,Name\nVX,Wrong\n')

    def test_postgres_values_cannot_escape_literals(self):
        self.assertEqual("'Trader''s Fund'",labels.quote("Trader's Fund"))


if __name__=='__main__': unittest.main()
