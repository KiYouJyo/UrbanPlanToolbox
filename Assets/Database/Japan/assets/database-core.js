// Pure data operations shared by the map and regression tests.
export function normalizeCode(value) {
  const s=String(value??'').trim();
  if(!/^\d{1,6}$/.test(s))return '';
  if(s.length===6){const first=s.slice(0,5),sum=[6,5,4,3,2].reduce((a,w,i)=>a+w*Number(first[i]),0);let check=11-sum%11;if(check>=10)check=0;if(check!==Number(s[5]))return '';return first;}
  if(s.length<=2)return s.padStart(2,'0');
  return s.padStart(5,'0');
}
export function matchRows(rows,codeColumn,knownCodes){
  const seen=new Set(),matched=[],unmatched=[],duplicates=[];
  for(const row of rows){let code=normalizeCode(row[codeColumn]);if(!knownCodes.has(code)&&code.length===5&&code.endsWith("000")&&knownCodes.has(code.slice(0,2)))code=code.slice(0,2);
    if(!code||!knownCodes.has(code)){unmatched.push(row);continue;}
    if(seen.has(code)){duplicates.push(row);continue;}
    seen.add(code);matched.push({code,values:{...row}});
  }
  return {matched,unmatched,duplicates};
}
export function joinedProperties(feature,datasets){
  const p=feature.properties,out={code:p.officialCode,name:p.name,prefecture:p.prefecture,city:p.city,ward:p.ward,level:p.administrativeLevel,boundary_date:'2026-01-01'};
  datasets.forEach((d,i)=>{const values=d.rows.find(r=>r.code===p.officialCode)?.values;out[`d${i+1}_title`]=d.title;out[`d${i+1}_year`]=d.year;out[`d${i+1}_source`]=d.source;out[`d${i+1}_unit`]=d.unit;out[`d${i+1}_definition`]=d.definition;for(const field of d.fields)out[`d${i+1}_${field}`]=values?.[field]??null;});return out;
}
export function numeric(value){if(value===null||value===undefined||String(value).trim()==='')return null;const s=String(value).replace(/,/g,'').trim();return /^[-+]?(?:\d+\.?\d*|\.\d+)$/.test(s)&&Number.isFinite(Number(s))?Number(s):null;}
