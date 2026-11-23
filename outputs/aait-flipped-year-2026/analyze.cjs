const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');

const [input, out, octoberLedger] = process.argv.slice(2);
const days = JSON.parse(fs.readFileSync(input));
const features = {
    long_range_5: [1, ['range5']],
    long_range_15: [1, ['range15']],
    long_momentum_5: [1, ['momentum5']],
    long_momentum_15: [1, ['momentum15']],
    short_prior_duration: [-1, ['logDuration']],
    short_prior_event: [-1, ['mode']],
    long_combined: [1, ['range5', 'range15', 'momentum5', 'momentum15']],
    short_combined: [-1, ['logDuration', 'mode']],
};
const policies = ['no_filter', ...Object.keys(features), 'both_combined'];

function execute(day, allow) {
    let exitRow = 0;
    let blocked = 0;
    const taken = [];
    for (const trade of day.trades) {
        const candidate = trade.candidate;
        if (candidate.row + 1 <= exitRow || candidate.sign === blocked || !allow(candidate)) continue;
        taken.push(trade);
        exitRow = trade.exit_row;
        blocked = trade.stopped ? candidate.sign : 0;
    }
    return taken;
}

let verifiedOctoberTrades = null;
if (octoberLedger) {
    const october = days.filter(day => day.date.startsWith('2026-10'));
    const regression = october.flatMap(day => execute(day, () => true).map(trade => [
        day.date, trade.candidate.sign === 1 ? 'Long' : 'Short', trade.candidate.group,
        trade.candidate.row + 1, trade.entry_row, trade.exit_row, trade.entry_price, trade.candidate.threshold,
        trade.best_price, trade.exit_price, trade.stopped ? 'Trailing stop' : 'Session flatten', trade.gross,
    ].join(',')));
    const [header, ...lines] = fs.readFileSync(octoberLedger, 'utf8').trim().split(/\r?\n/);
    const columns = header.split(',');
    const keys = ['date','side','dc_group','dc_row','entry_row','exit_row','entry_price','dc_threshold',
        'best_price','exit_price','exit_reason','gross_pnl_usd'];
    assert.deepEqual(regression, lines.map(line => {
        const cells = line.split(',');
        return keys.map((key, i) => {
            const cell = cells[columns.indexOf(key)];
            return [0,1,10].includes(i) ? cell : Number(cell);
        }).join(',');
    }));
    verifiedOctoberTrades = regression.length;
}

function fit(examples, names) {
    const modes = [...new Set(examples.map(example => example.candidate.prior_mode))].sort((a,b) => a-b);
    const extract = c => names.flatMap(name => {
        if (name === 'mode') return modes.map(mode => Number(mode === c.prior_mode));
        if (name === 'logDuration') return [Math.log1p(c.prior_minutes)];
        return [c[name]];
    });
    const raw = examples.map(example => extract(example.candidate));
    const mean = raw[0].map((_, i) => raw.reduce((sum, x) => sum + x[i], 0) / raw.length);
    const scale = mean.map((m, i) => Math.sqrt(raw.reduce((sum,x) => sum + (x[i]-m)**2,0)/raw.length) || 1);
    const normalize = x => [1,...x.map((v,i) => (v-mean[i])/scale[i])];
    const matrix = raw.map(normalize);
    const weights = matrix[0].map(() => 0);
    const dot = x => x.reduce((sum,v,i) => sum + v*weights[i],0);
    for (let step = 0; step < 300; step++) {
        const gradient = weights.map((w,i) => i ? .05*w : 0);
        matrix.forEach((x,i) => {
            const residual = 1/(1+Math.exp(-dot(x))) - Number(examples[i].hit);
            x.forEach((v,j) => { gradient[j] += residual*v/matrix.length; });
        });
        weights.forEach((w,i) => { weights[i] = w - .4*gradient[i]; });
    }
    const scores = matrix.map(dot);
    const sorted = scores.toSorted((a,b) => a-b);
    // Select a score cutoff on the training set only. Optimize balanced accuracy.
    const hits = examples.filter(example => example.hit).length;
    assert(hits > 0 && hits < examples.length);
    let cutoff = sorted[Math.floor(sorted.length / 2)];
    let best = -Infinity;
    for (const quantile of [.2,.3,.4,.5,.6,.7]) {
        const boundary = sorted[Math.floor(quantile * (sorted.length - 1))];
        let tp = 0, tn = 0;
        examples.forEach((example,i) => {
            if (scores[i] >= boundary && example.hit) tp++;
            if (scores[i] < boundary && !example.hit) tn++;
        });
        const accuracy = tp/hits + tn/(examples.length-hits);
        if (accuracy > best) {
            best = accuracy;
            cutoff = boundary;
        }
    }
    return {allow: candidate => dot(normalize(extract(candidate))) >= cutoff, samples: examples.length, hits, cutoff};
}

const months = [...new Set(days.map(day => day.date.slice(0,7)))];
const records = [];
const fitted = {};
for (const [index, month] of months.entries()) {
    const evaluation = days.filter(day => day.date.startsWith(month));
    if (index < 3) continue;
    const earlier = days.filter(day => day.date.slice(0,7) < month).flatMap(day => day.trades);
    const models = Object.fromEntries(Object.entries(features).map(([name,[side,names]]) =>
        [name,fit(earlier.filter(t => t.candidate.sign === side), names)]));
    fitted[month] = Object.fromEntries(Object.entries(models).map(([name,{samples,hits,cutoff}]) => [name,{samples,hits,cutoff}]));
    for (const policy of policies) {
        const allow = candidate => {
            if (policy === 'no_filter') return true;
            if (policy === 'both_combined') return models[candidate.sign === 1 ? 'long_combined' : 'short_combined'].allow(candidate);
            return candidate.sign !== features[policy][0] || models[policy].allow(candidate);
        };
        for (const day of evaluation) {
            for (const trade of execute(day, allow)) {
                records.push({policy, date: day.date, contract: day.contract, side: trade.candidate.sign === 1 ? 'Long' : 'Short',
                    group: trade.candidate.group, threshold: trade.candidate.threshold,
                    entry_row: trade.entry_row, exit_row: trade.exit_row, entry_price: trade.entry_price,
                    exit_price: trade.exit_price, best_price: trade.best_price, hit: trade.hit, gross: trade.gross});
            }
        }
    }
}

function summarize(trades) {
    const hits = trades.filter(trade => trade.hit).length;
    const gross = trades.reduce((sum,trade) => sum + trade.gross,0);
    return {trades: trades.length, hits, hit_rate: hits/trades.length || 0, gross,
        gross_per_trade: gross/trades.length || 0};
}
function sides(trades) {
    return Object.fromEntries(['Total','Long','Short'].map(side =>
        [side,summarize(trades.filter(t => side === 'Total' || (t.side || (t.candidate.sign === 1 ? 'Long' : 'Short')) === side))]));
}
const report = {
    sessions: days.length, es_ticks: days.reduce((sum,d) => sum+d.rows,0),
    signals: days.reduce((sum,d) => sum+d.signal_count,0),
    early_end_dates: days.filter(day => !day.has_cutoff_tick).map(day => day.date),
    verified_october_baseline_trades: verifiedOctoberTrades,
    full_year_no_filter: sides(days.flatMap(day => execute(day, () => true))),
    training_warmup_months: months.slice(0,3),
    held_out_months: months.slice(3,-1),
    discovery_month: months.at(-1),
    models: fitted,
    results: Object.fromEntries(policies.map(policy => {
        const all = records.filter(t => t.policy === policy);
        return [policy,{
            out_of_sample: sides(all.filter(t => !t.date.startsWith('2026-10'))),
            october: sides(all.filter(t => t.date.startsWith('2026-10'))),
            monthly: Object.fromEntries(months.slice(3).map(month => [month,sides(all.filter(t => t.date.startsWith(month)))])),
        }];
    })),
};

fs.writeFileSync(path.join(out,'comparison.json'), JSON.stringify(report,null,2)+'\n');
fs.writeFileSync(path.join(out,'trades.csv'), Object.keys(records[0]).join(',')+'\n'+records.map(t => Object.values(t).join(',')).join('\n')+'\n');
console.log('year',report.full_year_no_filter,'early end',report.early_end_dates);
for (const [policy, result] of Object.entries(report.results)) {
    const columns = Object.values(result.out_of_sample).map(r => [r.trades,r.hits,+((100*r.hit_rate).toFixed(1)),r.gross]);
    console.log(policy,JSON.stringify(columns),'October',result.october.Total);
}
