import {localize} from './i18n.js';
import {PREFECTURES, normalizeCollection, makeMunicipalityCollection, bboxOfFeature,
  estimateAreaKm2, featureCollection} from './data.js';

/**
 * UI is intentionally browser-native and dependency-light. GeoJSON is hosted alongside static assets; later WinUI 3/WebView2 can replace the data adapter.
 */
const $ = id => document.getElementById(id);
const state = {
  map:null, ready:false, pref:null, detail:null, coarse:null,
  level:'auto', effectiveLevel:'prefecture', basemap:'pale', showBoundaries:true, showFills:true,
  hovered:{kind:null,uid:null}, selected:null, selectedKind:null,
  searchResults:[], focusedResult:0, featureLookup:new Map(),
  areaCache:new Map(), unionFailures:0,
};

const TEXT = {
  prefecture:'都道府县',municipality:'市町村 / 特别区',ward:'最细行政区',
  'designated-city':'政令指定都市',
};
const QS = [
  {name:'北海道', lon:142.0,lat:43.2,zoom:5},
  {name:'東京都', lon:139.69,lat:35.68,zoom:9},
  {name:'大阪府', lon:135.5,lat:34.7,zoom:9},
  {name:'熊本県', lon:130.73,lat:32.78,zoom:8},
  {name:'福岡県', lon:130.43,lat:33.56,zoom:8.3},
  {name:'沖縄県', lon:127.68,lat:26.21,zoom:8}
];
const gsiBase = (type) => `https://cyberjapandata.gsi.go.jp/xyz/${type}/{z}/{x}/{y}.png`;
let toastTimeout;

function escapeHtml(str) {
  return String(str ?? '').replace(/[&<>"']/g, c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
}
function toast(msg){const el=$('toast');el.textContent=msg;el.classList.add('show');clearTimeout(toastTimeout);toastTimeout=setTimeout(()=>el.classList.remove('show'),3100);}
function loading(text,percent){$('loader-description').textContent=text;$('progress-inner').style.width=percent+'%';localize();}
function dismissLoader(){const el=$('loader-overlay');el.classList.add('hidden');setTimeout(()=>{el.style.display='none'},400);}
function titleFor(f,kind){return f?.properties.name||TEXT[kind]||'行政区划';}
function typeFor(f,kind){if(kind==='prefecture')return '都道府県';if(f.properties.ward)return '政令指定都市・行政区';if(f.properties.groupedCount>1)return '政令指定都市';if(f.properties.city?.endsWith('区'))return '東京都特別区';return '市・町・村';}
function getDatasetStatus(){return state.pref?.features.length&&state.detail?.features.length ? '已就绪 · 2026' : '正在加载';}

let baseLayer, geoLayers={}, selectionLayer;
function initMap(){
  const map=L.map('map',{zoomControl:false,attributionControl:false,preferCanvas:true,minZoom:3,maxZoom:16}).setView([36.6,136.4],4.3);
  state.map=map;
  baseLayer=L.tileLayer(gsiBase('pale'),{maxZoom:18}).addTo(map);
  // Boundary loading is independent of raster tile availability.
  map.on('zoomend',()=>{$('map-zoom-label').textContent=`Z ${map.getZoom().toFixed(1)}`;if(state.level==='auto')setLevelVisibility();});
  map.flyToCompat=({center,zoom})=>map.flyTo([center[1],center[0]],zoom);
  return Promise.resolve();
}
function normalStyle(kind){return {color:kind==='pref'?'#315b72':'#238c89',weight:kind==='pref'?1.2:.8,opacity:state.showBoundaries? .9:0,fillColor:'#49b7a6',fillOpacity:state.showFills?.15:0};}
function addGeoSourcesAndLayers(){
  for(const [key,data] of [['pref',state.pref],['coarse',state.coarse],['detail',state.detail]]){
    geoLayers[key]=L.geoJSON(data,{style:()=>normalStyle(key),onEachFeature:(f,layer)=>{
      layer.on('mouseover',()=>layer.setStyle({color:'#f5a847',weight:3,opacity:1}));
      layer.on('mouseout',()=>{layer.setStyle(normalStyle(key));document.dispatchEvent(new Event('map-hover-ended'));});
      layer.on('click',e=>{L.DomEvent.stopPropagation(e);selectFeature(f,key==='pref'?'prefecture':key,false);});
    }});
  }
  selectionLayer=L.geoJSON(null,{interactive:false,style:{color:'#d37a25',weight:3,fillColor:'#e9a354',fillOpacity:.28}}).addTo(state.map);
}
function effectiveLevel(){
  if(state.level!=='auto')return state.level;
  const z=state.map.getZoom();
  return z < 6.75 ? 'prefecture' : z < 9.35 ? 'municipality' : 'ward';
}
function setLevelVisibility(){
  if(!state.ready)return;
  const now=effectiveLevel();state.effectiveLevel=now;
  const key={prefecture:'pref',municipality:'coarse',ward:'detail'}[now];
  for(const [k,layer] of Object.entries(geoLayers)){
    if(k===key){layer.setStyle(()=>normalStyle(k));if(!state.map.hasLayer(layer))layer.addTo(state.map);}
    else if(state.map.hasLayer(layer))state.map.removeLayer(layer);
  }
  selectionLayer?.bringToFront();
  $('active-level-text').textContent=`当前显示：${TEXT[now]}`;
  document.dispatchEvent(new Event('map-style-changed'));
  $('legend-text').textContent={prefecture:'都道府県境界',municipality:'市町村境界',ward:'市区町村・行政区境界'}[now];localize();
}
function selectFeature(f,kind,fit=true){
  if(!state.ready)return;
  state.selected=f;
  state.selectedKind=kind;
  selectionLayer.clearLayers();selectionLayer.addData(f);selectionLayer.bringToFront();
  $('clear-selection').hidden=false;
  renderInspector(f,kind);
  document.querySelector('.inspector')?.classList.add('is-open');
  if(fit)zoomToFeature(f);
  if(kind==='prefecture' && state.level!=='prefecture' && state.level!=='auto')setLevel('prefecture');
  if(kind==='detail' && state.level==='prefecture')setLevel('ward');
  if(kind==='coarse' && state.level==='prefecture')setLevel('municipality');
  localize();
  document.dispatchEvent(new CustomEvent('area-selected',{detail:f}));
}
function clearSelection(){
  state.selected=null;state.selectedKind=null;
  selectionLayer?.clearLayers();
  $('clear-selection').hidden=true;
  $('inspector-content').innerHTML=emptyInspectorMarkup();
  document.querySelector('.inspector')?.classList.remove('is-open');
  document.dispatchEvent(new Event('selection-cleared'));
}
function emptyInspectorMarkup(){return `<div class="empty-inspector"><div class="empty-symbol"><svg width="96" height="96" viewBox="0 0 96 96" fill="none"><path d="m22 28 24-14 28 17-6 35-23 17-27-20 4-35Z" stroke="currentColor" stroke-width="1.6"/><path d="m22 28 23 19 29-16M45 47v36M18 63l27-16" stroke="currentColor" stroke-width="1.6" stroke-dasharray="4 5"/><circle cx="45" cy="47" r="6" fill="#0a8c8c" stroke="white" stroke-width="2"/></svg></div><h2>探索日本的行政区划</h2><p>在地图上点击一个行政区域，查看名称、行政代码、所属层级及面积等详细信息。</p><div class="empty-hint"><span>◉</span> 支持按区划等级精确选取</div></div>`;}

function zoomToFeature(f){
  const box=bboxOfFeature(f);
  if(!box)return;
  const map=state.map;
  if(box[0][0]===box[1][0])return;
  map.fitBounds([[box[0][1],box[0][0]],[box[1][1],box[1][0]]],{padding:[60,70],maxZoom:10});
}
function getArea(f){
  if(!state.areaCache.has(f.id))state.areaCache.set(f.id,estimateAreaKm2(f));
  return state.areaCache.get(f.id);
}
function renderInspector(f,kind){
  const p=f.properties;
  const name=titleFor(f,kind);
  const kindTitle=typeFor(f,kind);
  const area=getArea(f);
  const num=area<1 ? area.toFixed(2) : area<100?area.toFixed(1):Math.round(area).toLocaleString('zh-CN');
  const code=p.officialCode||'—';
  const row=(label,val)=>val?`<div class="attribute-row"><span>${escapeHtml(label)}</span><strong>${escapeHtml(val)}</strong></div>`:'';
  const displayName=p.ward?`${p.city} · ${p.ward}`:(kind==='coarse'?p.city||p.name:name);
  $('inspector-content').innerHTML=`
    <div class="feature-card">
      <span class="feature-badge">◈ ${escapeHtml(kindTitle)}</span>
      <div class="feature-geo">SELECTED ADMINISTRATIVE AREA</div>
      <h2 class="feature-name">${escapeHtml(displayName)}</h2>
      <p class="feature-subtitle">${escapeHtml(p.prefecture||'日本')} ${p.county?`/ ${escapeHtml(p.county)}`:''} · 2026年1月1日</p>
      <div class="feature-stat">
        <div class="stat-box"><span class="stat-label">行政代码</span><div class="stat-val">${escapeHtml(code)}</div></div>
        <div class="stat-box"><span class="stat-label">几何面积（近似）</span><div class="stat-val">${num}<small> km²</small></div></div>
      </div>
      <div class="feature-section">属性信息 / ATTRIBUTES</div>
      <div class="attribute-table">
        ${row('名称',name)}
        ${row('都道府県',p.prefecture)}
        ${row('振興局・支庁',p.branch)}
        ${row('郡',p.county)}
        ${row('市町村・特别区',p.city)}
        ${row('政令市行政区',p.ward)}
        ${row('市级包含的行政区',p.groupedCount>1?String(p.groupedCount)+' 区':'')}
        ${row('行政代码',p.officialCode||'未提供')}
        ${row('数据年份','2026年')}
      </div>
      <div class="feature-actions">
        <button id="inspector-zoom">⌗ 定位区域</button>
        <button id="inspector-export">⇩ 导出 GeoJSON</button>
        <button id="inspector-copy">⧉ 复制代码</button>
        <button id="inspector-close">× 取消选中</button>
      </div>
      <p class="feature-notice">面积由当前简化几何估算，并非日本官方法定面积。本数据未附带人口和家庭数。部分行政界线属于原数据注明的暂定边界。</p>
    </div>`;
  $('inspector-zoom').addEventListener('click',()=>zoomToFeature(f));
  $('inspector-export').hidden=true;
  $('inspector-copy').addEventListener('click',()=>copyCode(p.officialCode));
  $('inspector-close').addEventListener('click',clearSelection);
}
async function copyCode(code){
  if(!code){toast('该合并区划未提供单独的官方代码');return;}
  try{await navigator.clipboard.writeText(code);toast(`已复制：${code}`);}catch{toast(`行政代码：${code}`);}
}
function exportSelected(){
  if(!state.selected)return;
  const f=state.selected;
  const out={type:'FeatureCollection',features:[{type:'Feature',properties:{...f.properties,
    source:'MLIT N03-2026; derivative by geo.maderaojen.me'},geometry:f.geometry}]};
  const blob=new Blob([JSON.stringify(out)],{type:'application/geo+json'});
  const url=URL.createObjectURL(blob);
  const a=document.createElement('a');a.href=url;a.download=`Japan_${f.properties.officialCode||f.properties.name||'area'}_2026.geojson`;a.click();
  setTimeout(()=>URL.revokeObjectURL(url),1000);
  toast('已导出当前选中区划的 GeoJSON');
}

function setLevel(level){
  state.level=level;
  document.querySelectorAll('[data-level]').forEach(b=>{b.classList.toggle('active',b.dataset.level===level);b.setAttribute('aria-pressed',b.dataset.level===level?'true':'false');});
  setLevelVisibility();
}
function setBaseMap(type){
  state.basemap=type;
  document.querySelectorAll('[data-basemap]').forEach(b=>{b.classList.toggle('active',b.dataset.basemap===type);b.setAttribute('aria-pressed',b.dataset.basemap===type?'true':'false');});
  if(baseLayer)state.map.removeLayer(baseLayer);
  if(type!=='blank')baseLayer=L.tileLayer(gsiBase(type==='pale'?'pale':'std'),{maxZoom:18}).addTo(state.map);
}

const ALIASES={东京:'東京都',東京:'東京都',大阪:'大阪府',京都:'京都府',熊本:'熊本県',冲绳:'沖縄県',沖繩:'沖縄県',福冈:'福岡県',广岛:'広島県',长崎:'長崎県',神奈川:'神奈川県',札幌:'札幌市',名古屋:'名古屋市',横滨:'横浜市',仙台:'仙台市'};
function cleanQuery(s){const val=String(s||'').trim();return ALIASES[val]||val;}
function findResults(query){
  const input=cleanQuery(query).toLowerCase();
  if(!input)return [];
  const targets=[...state.pref.features.map(f=>({feature:f,kind:'prefecture'})),
    ...state.coarse.features.map(f=>({feature:f,kind:'coarse'})),
    ...state.detail.features.filter(f=>f.properties.ward).map(f=>({feature:f,kind:'detail'}))];
  return targets.filter(x=>x.feature.properties.nameSearch.includes(input) ||
    x.feature.properties.name.toLowerCase().includes(input)).map(x=>{
      const p=x.feature.properties;
      const s=(p.name===input?0:p.name.startsWith(input)?1:p.city===input?1:p.prefecture===input?2:4) +
        (x.kind==='prefecture' ? 0 : x.kind==='coarse'? .2 : .4);
      return {...x,rank:s};
    }).sort((a,b)=>a.rank-b.rank||a.feature.properties.name.length-b.feature.properties.name.length).slice(0,11);
}
function renderSuggestions(){
  const el=$('suggestions');
  const query=$('search').value;
  if(!query.trim()){el.classList.add('hidden');return;}
  const results=findResults(query);
  state.searchResults=results;
  state.focusedResult=0;
  if(!results.length){el.innerHTML='<div class="suggestion-empty">没有匹配的区划，请尝试日语名称或行政代码</div>';}
  else{el.innerHTML=results.map((x,i)=>{
    const p=x.feature.properties;
    return `<button class="suggestion-item ${i===0?'current':''}" data-result="${i}" role="option">
      <div><div class="suggestion-name">${escapeHtml(p.name)}</div><div class="suggestion-sub">${escapeHtml(p.prefecture)} · ${escapeHtml(typeFor(x.feature,x.kind))}</div></div>
      <div class="suggestion-code">${escapeHtml(p.officialCode||'')}</div></button>`;
  }).join('');}
  el.classList.remove('hidden');
  el.querySelectorAll('[data-result]').forEach(b=>b.addEventListener('click',()=>chooseSuggestion(Number(b.dataset.result))));
}
function chooseSuggestion(idx=0){
  const x=state.searchResults[idx];
  if(!x)return;
  $('suggestions').classList.add('hidden');
  $('search').value=x.feature.properties.name;
  if(x.kind==='prefecture')setLevel('prefecture');
  else if(x.kind==='coarse')setLevel('municipality');
  else setLevel('ward');
  selectFeature(x.feature,x.kind,true);
}

function bindControls(){
  document.querySelectorAll('[data-level]').forEach(b=>b.addEventListener('click',()=>setLevel(b.dataset.level)));
  document.querySelectorAll('[data-basemap]').forEach(b=>b.addEventListener('click',()=>setBaseMap(b.dataset.basemap)));
  $('boundaries-toggle').addEventListener('change',e=>{state.showBoundaries=e.target.checked;setLevelVisibility();});
  $('fills-toggle').addEventListener('change',e=>{state.showFills=e.target.checked;setLevelVisibility();});
  $('reset-map').addEventListener('click',()=>state.map.flyToCompat({center:[136.4,36.6],zoom:4.3,duration:800}));
  $('zoom-in').addEventListener('click',()=>state.map.zoomIn());
  $('zoom-out').addEventListener('click',()=>state.map.zoomOut());
  $('clear-selection').addEventListener('click',clearSelection);
  document.querySelectorAll('[data-place]').forEach(b=>b.addEventListener('click',()=>{
    const place=b.dataset.place;
    const f=state.pref?.features.find(f=>f.properties.name===place||f.properties.prefecture===place);
    setLevel('prefecture');
    if(f)selectFeature(f,'prefecture',true);
    else{
      const loc=QS.find(q=>q.name===place);
      if(loc)state.map.flyToCompat({center:[loc.lon,loc.lat],zoom:loc.zoom,duration:700});
    }
  }));
  $('search').addEventListener('input',renderSuggestions);
  $('search').addEventListener('focus',renderSuggestions);
  $('search').addEventListener('keydown',e=>{
    if(e.key==='Escape'){$('suggestions').classList.add('hidden');$('search').blur();}
    if(e.key==='Enter'){e.preventDefault();chooseSuggestion(state.focusedResult);}
    if(['ArrowDown','ArrowUp'].includes(e.key)){
      e.preventDefault();const n=state.searchResults.length;
      if(!n)return;
      state.focusedResult=(state.focusedResult+(e.key==='ArrowDown'?1:-1)+n)%n;
      $('suggestions').querySelectorAll('[data-result]').forEach((b,i)=>b.classList.toggle('current',i===state.focusedResult));
    }
  });
  document.addEventListener('keydown',e=>{
    const focused=['INPUT','TEXTAREA'].includes(document.activeElement?.tagName);
    if((e.key==='/'&&!focused)||(e.ctrlKey&&e.key.toLowerCase()==='k')){
      e.preventDefault();$('search').focus();$('search').select();
    }
  });
  document.addEventListener('click',e=>{
    if(!e.target.closest('.search-wrap'))$('suggestions').classList.add('hidden');
  });
}

async function fetchDataset(name,label){
  const file = name === 'prefectures' ? 'prefectures.geojson' : 'municipalities.geojson';
  const response = await fetch(`./data/${file}`, {cache:'default'});
  if(!response.ok) {
    throw new Error(`${label}加载失败（HTTP ${response.status}）。请稍后刷新重试。`);
  }
  const payload = await response.json();
  if(payload.type !== 'FeatureCollection') throw new Error(`${label}文件格式不正确`);
  return payload;
}

async function boot(){
  bindControls();
  if(!window.L)throw Error('地图库加载失败，请刷新重试。');
  loading('正在初始化地图画布和地理院底图…',18);
  await initMap();
  loading('正在加载都道府县边界数据…',32);
  const prefRaw=await fetchDataset('prefectures','都道府县数据');
  state.pref=normalizeCollection(prefRaw,'prefectures');
  loading('正在加载 2026 年市区町村边界数据…',57);
  const detailRaw=await fetchDataset('municipalities','市区町村数据');
  state.detail=normalizeCollection(detailRaw,'municipalities');
  loading('正在构建政令指定都市的市级边界…',78);
  await new Promise(r=>setTimeout(r,30));
  const union=window.polygonClipping?.union ? window.polygonClipping : null;
  const coarse=makeMunicipalityCollection(state.detail,union);
  state.coarse=coarse.collection;
  state.unionFailures=coarse.unionFailures;
  loading('正在生成地图图层与行政区划检索索引…',91);
  await new Promise(r=>setTimeout(r,30));
  for(const group of [state.pref,state.detail,state.coarse]){
    group.features.forEach(f=>state.featureLookup.set(f.properties.uid,f));
  }
  // Native <div> controls are ready. Enable the visibility controls only after source creation.
  addGeoSourcesAndLayers();
  state.ready=true;
  setLevelVisibility();
  $('search').disabled=false;
  $('data-availability').textContent='地图数据已加载';
  $('top-status').textContent='边界数据已就绪';
  $('pref-count').textContent=state.pref.features.length.toLocaleString('zh-CN');
  $('muni-count').textContent=state.detail.features.length.toLocaleString('zh-CN');
  $('map-zoom-label').textContent=`Z ${state.map.getZoom().toFixed(1)}`;
  loading('地图已准备完毕',100);
  dismissLoader();
  document.dispatchEvent(new Event('database-ready'));
  localize();
  if(state.unionFailures)console.warn('部分政令市行政区边界使用简单组合代替融合，数量：',state.unionFailures);
}

function showFatal(error){
  console.error(error);
  $('loader-overlay').classList.remove('hidden');
  $('loader-overlay').style.display='flex';
  $('loader-description').innerHTML=`<span style="color:#b14a3b">${escapeHtml(error.message||String(error))}</span><br>请刷新网页，若仍失败，请稍后重试。`;
  $('progress-inner').style.width='100%';
  $('progress-inner').style.background='#d56e55';
  $('top-status').textContent='加载失败';
  $('data-availability').textContent='数据不可用';
}

document.addEventListener('DOMContentLoaded',()=>boot().catch(showFatal));

export {state, $, toast, escapeHtml, renderInspector, selectFeature, geoLayers, selectionLayer, normalStyle, setLevelVisibility};
import './database.js';
