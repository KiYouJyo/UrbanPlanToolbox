import test from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
const source=await readFile(new URL('../Assets/Database/Japan/assets/database-core.js',import.meta.url),'utf8');
const {normalizeCode,matchRows,joinedProperties,numeric}=await import('data:text/javascript;base64,'+Buffer.from(source).toString('base64'));
test('codes preserve zeros and validate the six-digit check digit',()=>{
 assert.equal(normalizeCode('011002'),'01100');assert.equal(normalizeCode('431001'),'43100');assert.equal(normalizeCode('431002'),'');assert.equal(normalizeCode(1100),'01100');assert.equal(normalizeCode('1'),'01');assert.equal(normalizeCode('foo'),'');
});
test('matching separates missing, unknown and duplicate rows',()=>{
 const r=matchRows([{code:'43101',p:'0'},{code:'43101',p:'9'},{code:'99999'},{code:''}], 'code',new Set(['43101']));
 assert.equal(r.matched.length,1);assert.equal(r.duplicates.length,1);assert.equal(r.unmatched.length,2);assert.equal(r.matched[0].values.p,'0');
});
test('joins retain dataset identity, original fields and missing versus zero',()=>{
 const datasets=[{id:'one',title:'人口',year:'2020',source:'Test',unit:'人',definition:'常住人口',fields:['人口'],rows:[{code:'43101',values:{人口:0}}]}];
 const a=joinedProperties({properties:{officialCode:'43101',name:'中央区'}},datasets),b=joinedProperties({properties:{officialCode:'43102',name:'東区'}},datasets);
 assert.equal(a.d1_人口,0);assert.equal(b.d1_人口,null);assert.equal(a.d1_year,'2020');assert.equal(a.boundary_date,'2026-01-01');
});
test('numeric conversion treats blank and suppression symbols as missing',()=>{
 for(const v of ['',null,' ','—','秘匿'])assert.equal(numeric(v),null);
 assert.equal(numeric('0'),0);assert.equal(numeric('1,234'),1234);assert.equal(numeric('-2.5'),-2.5);
});
test('designated-city ward grouping uses the explicit parent code',async()=>{
 const src=await readFile(new URL('../Assets/Database/Japan/assets/data.js',import.meta.url),'utf8');
 const {makeMunicipalityCollection}=await import('data:text/javascript;base64,'+Buffer.from(src).toString('base64'));
 const wards=['43101','43102'].map((code,i)=>({id:code,properties:{uid:code,parentKey:'43|熊本市',city:'熊本市',ward:i?'東区':'中央区',officialCode:code},geometry:{type:'Polygon',coordinates:[[[0,0],[1,0],[1,1],[0,0]]]}}));
 const result=makeMunicipalityCollection({features:wards});assert.equal(result.collection.features[0].properties.officialCode,'43100');
});
test('prefecture statistical codes resolve to prefecture boundaries',()=>{
 const r=matchRows([{code:'43000'},{code:'010006'}],'code',new Set(['43','01']));assert.deepEqual(r.matched.map(r=>r.code),['43','01']);
});
