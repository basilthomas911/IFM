import fs from 'node:fs/promises';
import {Workbook, SpreadsheetFile} from '@oai/artifact-tool';
const report = JSON.parse(await fs.readFile(new URL('./verified/analysis.json',import.meta.url),'utf8'));
const wb = Workbook.create();
const daily = wb.worksheets.add('Daily PnL');
const trades = wb.worksheets.add('Trades');
const money = '#,##0.00;[Red](#,##0.00);"—"';
const number = '0.00;[Red](0.00);"—"';
function base(s,range){
  s.showGridLines=false;
  s.getRange(range).format.font={name:'Arial',size:10,color:'#182B40'};
  s.getRange(range).format.rowHeight=23;
}
function header(s,range){
  s.getRange(range).format.fill='#193B58';
  s.getRange(range).format.font={bold:true,color:'#FFFFFF'};
  s.getRange(range).format.rowHeight=32;
  s.getRange(range).format.wrapText=true;
}
function pnl(s,range){
  s.getRange(range).setNumberFormat(money);
  s.getRange(range).conditionalFormats.add('cellIs',{operator:'lessThan',formula:0,format:{font:{color:'#B42318'}}});
  s.getRange(range).conditionalFormats.add('cellIs',{operator:'greaterThan',formula:0,format:{font:{color:'#147D55'}}});
}
base(daily,'A1:H39'); base(trades,'A1:P55');
daily.getRange('A1').values=[['Daily ES reversion | October 2026']];
daily.getRange('A1').format.font={size:16,bold:true};
daily.getRange('A3:B5').values=[['Instrument','ES Dec 2026'],['Contracts',1],['USD / point',50]];
daily.getRange('B4:B5').format.font={color:'#0000FF'};
daily.getRange('D3').values=[['Entries: 03:00–15:45 New York; flat at 15:45']];
daily.getRange('D4').values=[['Gross trade-price estimate; no fees, spread or latency']];
daily.getRange('D5').values=[['Entry: second reversal after qualified overshoot; next tick']];
daily.getRange('D6').values=[['Stop: frozen extreme; profit: overshoot after DC or DC return']];
daily.getRange('D7').values=[['15:45 exits use last known price at/before cutoff']];
daily.getRange('A9:H9').values=[['NY trade date','Trades','Wins','Losses','Points','Gross P&L ($)','Running P&L ($)','Stops']];
header(daily,'A9:H9');
const last=report.trades.length+5;
const dr=report.daily.map((d)=>[d.trade_date,null,null,null,null,null,null,null]);
daily.getRange(`A10:H${dr.length+9}`).values=dr;
const tdate=`Trades!$A$6:$A$${last}`, pts=`Trades!$L$6:$L$${last}`, dollars=`Trades!$M$6:$M$${last}`;
for(let i=0;i<dr.length;i++){
 const r=i+10;
 daily.getRange(`B${r}:H${r}`).formulas=[[
  `=COUNTIFS(${tdate},A${r})`,
  `=COUNTIFS(${tdate},A${r},${pts},">0")`,
  `=COUNTIFS(${tdate},A${r},${pts},"<0")`,
  `=SUMIFS(${pts},${tdate},A${r})`,
  `=SUMIFS(${dollars},${tdate},A${r})`,
  `=SUM($F$10:F${r})`,
  `=COUNTIFS(${tdate},A${r},Trades!$K$6:$K$${last},"Protective stop")`
 ]];
 if(i%2===0) daily.getRange(`A${r}:H${r}`).format.fill='#F0F5F9';
}
const total=dr.length+10;
daily.getRange(`A${total}`).values=[['MONTH TOTAL']];
for(const c of ['B','C','D','E','F','H'])daily.getRange(`${c}${total}`).formulas=[[`=SUM(${c}10:${c}${total-1})`]];
daily.getRange(`G${total}`).formulas=[[`=G${total-1}`]];
header(daily,`A${total}:H${total}`);
daily.getRange(`E10:E${total}`).setNumberFormat(number);
pnl(daily,`F10:G${total-1}`);
daily.getRange(`F${total}:G${total}`).setNumberFormat(money);
daily.getRange(`A${total+2}`).values=[['Source: verified ITI replay e1ce5fbc17b7; 5,288 Daily events; 10,800,331 joined ES ticks.']];
daily.getRange(`A${total+3}`).values=[['Overnight state retained; reversal counting starts at 03:00. One position and one attempt per excursion.']];
daily.getRange(`A${total+4}`).values=[['Qualification: existing extended-overshoot event; no extra minimum or mandatory velocity filter.']];
daily.getRange(`A${total+5}`).values=[['Research baseline: cold start, no September warmup; actual execution and risk controls not simulated.']];
daily.getRange(`A${total+2}:H${total+5}`).format.font={size:9,color:'#586A7C'};
daily.getRange('A:A').format.columnWidth=17;
daily.getRange('B:D').format.columnWidth=10;
daily.getRange('E:E').format.columnWidth=12;
daily.getRange('F:G').format.columnWidth=21;
daily.getRange('H:H').format.columnWidth=11;
daily.freezePanes.freezeRows(9);

trades.getRange('A1').values=[['Individual trades | New York times']];
trades.getRange('A1').format.font={size:16,bold:true};
trades.getRange('A3').values=[['One ES; trade-price fill proxies. Session flatten is a 15:45 mark, not a guaranteed executable fill.']];
trades.getRange('A4').values=[['DC = direction change. Negative DC-return P&L is retained when the observed crossing price gaps.']];
trades.getRange('A5:P5').values=[['Trade date','Trade #','Side','Entry (NY)','Entry price','Stop price','DC (NY)','DC price','Exit (NY)','Exit price','Exit reason','Points','Gross P&L ($)','Risk (points)','Setup group','15:45 price as of']];
header(trades,'A5:P5');
const clock=s=>s ? s.slice(11,26) : '';
trades.getRange(`A6:P${last}`).values=report.trades.map((t,i)=>[
 t.trade_date,i+1,t.side,clock(t.entry_time_ny),t.entry_price,t.stop_price,
 clock(t.dc_time_ny),t.dc_price,clock(t.exit_time_ny),t.exit_price,t.exit_reason,
 null,null,t.initial_risk_points,t.setup_group,
 t.exit_reason==='Session flatten'?clock(t.exit_price_reference_time_ny):''
]);
for(let i=0;i<report.trades.length;i++){
 const r=i+6;
 trades.getRange(`L${r}:M${r}`).formulas=[[
  `=IF(C${r}="Long",J${r}-E${r},E${r}-J${r})`,
  `=L${r}*'Daily PnL'!$B$4*'Daily PnL'!$B$5`
 ]];
 if(i%2===0) trades.getRange(`A${r}:P${r}`).format.fill='#F0F5F9';
}
trades.getRange('A:A').format.columnWidth=15;
trades.getRange('B:C').format.columnWidth=10;
for(const c of ['D','G','I','P'])trades.getRange(`${c}:${c}`).format.columnWidth=23;
for(const c of ['E','F','H','J','L','N','O'])trades.getRange(`${c}:${c}`).format.columnWidth=13;
trades.getRange('K:K').format.columnWidth=23;
trades.getRange('M:M').format.columnWidth=19;
trades.getRange(`E6:F${last}`).setNumberFormat('0.00');
trades.getRange(`H6:H${last}`).setNumberFormat('0.00');
trades.getRange(`J6:J${last}`).setNumberFormat('0.00');
trades.getRange(`L6:N${last}`).setNumberFormat(number);
pnl(trades,`M6:M${last}`);
trades.freezePanes.freezeRows(5);
trades.freezePanes.freezeColumns(3);
wb.recalculate();
daily.getRange('B5').values=[[100]];
wb.recalculate();
if(daily.getRange(`F${total}`).values[0][0]!==report.totals.gross_pnl_usd*2)throw new Error('Multiplier sensitivity failed');
daily.getRange('B5').values=[[50]];
wb.recalculate();
const calculated=daily.getRange(`F${total}`).values[0][0];
if(calculated!==report.totals.gross_pnl_usd)throw new Error(`Workbook reconciliation failed: ${calculated}`);
for(let i=0;i<report.daily.length;i++){
 if(daily.getRange(`F${i+10}`).values[0][0]!==report.daily[i].gross_pnl_usd)throw new Error('Daily reconciliation failed');
}
console.log((await wb.inspect({kind:'match',searchTerm:'#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A|#NUM!|#NULL!|#SPILL!|#CALC!',options:{useRegex:true,maxResults:20},maxChars:2000})).ndjson);
console.log((await wb.inspect({kind:'region',sheetId:'Daily PnL',range:`A${total}:H${total}`,maxChars:1500})).ndjson);
for(const [sheetName,range,name] of [['Daily PnL',`A1:H${total+5}`,'daily-preview.png'],['Trades','A1:M14','trades-preview.png']]){
 const img=await wb.render({sheetName,range,scale:1.5,format:'png'});
 await fs.writeFile(new URL(name,import.meta.url),new Uint8Array(await img.arrayBuffer()));
}
await (await SpreadsheetFile.exportXlsx(wb)).save(new URL('./October-2026-Daily-ES-Backtest.xlsx',import.meta.url).pathname.replace(/^\/([A-Z]:)/,'$1'));
console.log('Workbook exported; all daily P&L formulas reconcile.');
