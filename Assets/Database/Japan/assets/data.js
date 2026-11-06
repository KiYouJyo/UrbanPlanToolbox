/** Normalization layer for MLIT N03 GeoJSON and independently reworked N03 derivatives. */
export const PREFECTURES = [
  '北海道','青森県','岩手県','宮城県','秋田県','山形県','福島県',
  '茨城県','栃木県','群馬県','埼玉県','千葉県','東京都','神奈川県',
  '新潟県','富山県','石川県','福井県','山梨県','長野県','岐阜県',
  '静岡県','愛知県','三重県','滋賀県','京都府','大阪府','兵庫県',
  '奈良県','和歌山県','鳥取県','島根県','岡山県','広島県','山口県',
  '徳島県','香川県','愛媛県','高知県','福岡県','佐賀県','長崎県',
  '熊本県','大分県','宮崎県','鹿児島県','沖縄県'
];

const FIELD = {
  pref: ['N03_001','prefecture','prefecture_name','pref','pref_name','ken','ken_name','都道府県名','都道府県','prefName','PREF_NAME','PREFECTURE'],
  branch: ['N03_002','subprefecture','branch','振興局名','支庁'],
  county: ['N03_003','county','gun','郡名'],
  city: ['N03_004','municipality','municipality_name','municipalityName','city','city_name','cityName','shikuchoson','市区町村名','市区町村'],
  ward: ['N03_005','ward','ward_name','administrative_ward','行政区名','政令市行政区名'],
  code: ['N03_007','jis_code','JIS_CODE','lg_code','lgcode','municipality_code','admin_code','city_code','code','CODE','JCODE'],
  name: ['name_ja','name_jp','name','NAME','label','名称','NAME_JP','display_name'],
};

function getOne(props, fields) {
  for (const f of fields) {
    const v = props[f];
    if (v !== undefined && v !== null && String(v).trim() && String(v).trim() !== 'null') return String(v).trim();
  }
  return '';
}

export function cleanCode(value) {
  if (!value) return '';
  const digits = String(value).replace(/\D/g, '');
  if (digits.length === 6) return digits.slice(0, 5);
  return digits.length === 4 ? digits.padStart(5, '0') : digits;
}

function extractPref(str) {
  return PREFECTURES.find(p => str.startsWith(p)) || '';
}

function parentAndWard(text) {
  // 23 Tokyo special wards are standalone municipalities, not designated-city wards.
  const match = String(text || '').match(/^(.+?市)([^\s]+区)$/);
  return match ? {city: match[1], ward: match[2]} : null;
}

export function normalizeFeature(feature, kind, ordinal) {
  const original = feature.properties || {};
  let code = cleanCode(getOne(original, FIELD.code));
  const genericName = getOne(original, FIELD.name);
  let pref = getOne(original, FIELD.pref);
  if (!pref && genericName) pref = extractPref(genericName);
  if (!pref && code.length >= 2) pref = PREFECTURES[Number(code.slice(0, 2)) - 1] || '';
  const prefCode = (code.length >= 2 ? code.slice(0, 2) : String(PREFECTURES.indexOf(pref) + 1).padStart(2, '0'));

  let city = getOne(original, FIELD.city);
  let ward = getOne(original, FIELD.ward);
  const branch = getOne(original, FIELD.branch);
  const county = getOne(original, FIELD.county);
  const strippedGeneric = genericName.startsWith(pref) ? genericName.slice(pref.length) : genericName;

  if (kind === 'prefectures') {
    pref = pref || genericName;
    if (pref && !PREFECTURES.includes(pref)) {
      pref = PREFECTURES.find(p => p === pref || p.slice(0, -1) === pref) || pref;
    }
    code = code || (PREFECTURES.indexOf(pref) >= 0 ? String(PREFECTURES.indexOf(pref) + 1).padStart(2,'0') : '');
  } else {
    if (!city) city = strippedGeneric;
    if (city.startsWith(pref)) city = city.slice(pref.length);
    if (!ward) {
      const split = parentAndWard(city);
      if (split) {city = split.city; ward = split.ward;}
    }
    if (!city && ward) city = strippedGeneric || '不明';
  }

  const officialCode = code;
  const name = kind === 'prefectures' ? (pref || genericName || `都道府県 ${ordinal}`) :
    `${city}${ward}` || genericName || `行政区域 ${ordinal}`;
  const parentKey = `${prefCode}|${city || name}`;
  const uid = `${kind === 'prefectures' ? 'P' : 'D'}-${code || ordinal}-${ordinal}`;
  const properties = {
    uid, name, prefecture: pref, prefCode, city, ward, county, branch,
    officialCode, parentKey, administrativeLevel: kind === 'prefectures' ? 'prefecture' : ward ? 'ward' : 'municipality',
    nameSearch: `${pref} ${city} ${ward} ${county} ${name} ${code}`.toLowerCase(),
  };
  return {type:'Feature', id: uid, properties, geometry: feature.geometry};
}

export function normalizeCollection(raw, kind) {
  if (!raw || raw.type !== 'FeatureCollection' || !Array.isArray(raw.features)) {
    throw Error('数据源不是有效的 GeoJSON FeatureCollection');
  }
  const features = raw.features
    .filter(f => f && f.geometry && (f.geometry.type === 'Polygon' || f.geometry.type === 'MultiPolygon'))
    .map((f,i) => normalizeFeature(f,kind,i+1));
  if (features.length === 0) throw Error('GeoJSON 中没有可用的行政区划面要素');
  return {type:'FeatureCollection',features};
}

function toMultiCoordinates(geometry) {
  if (!geometry) return [];
  if (geometry.type === 'Polygon') return [geometry.coordinates];
  if (geometry.type === 'MultiPolygon') return geometry.coordinates;
  return [];
}

export function makeMunicipalityCollection(detail, polygonUnion = null) {
  const groups = new Map();
  for (const f of detail.features) {
    const p = f.properties;
    if (!groups.has(p.parentKey)) groups.set(p.parentKey, []);
    groups.get(p.parentKey).push(f);
  }
  let unionFailures = 0;
  const features = [];
  for (const group of groups.values()) {
    const first = group[0];
    const p = first.properties;
    if (group.length === 1) {
      features.push({ ...first, id: 'C-' + first.id,
        properties: {...p, uid:'C-'+p.uid, name:p.city || p.name, administrativeLevel:'municipality', ward:'', groupedCount:1}});
      continue;
    }
    const polygons = group.flatMap(f => toMultiCoordinates(f.geometry));
    let merged = polygons;
    if (polygonUnion && polygons.length > 1) {
      try {
        // The polygons in a ward group are independent. A union removes internal lines.
        merged = polygonUnion.union(...polygons.map(poly => [poly]));
        if (!Array.isArray(merged) || !merged.length) throw Error('Empty union');
      } catch (err) {
        unionFailures++;
        merged = polygons;
      }
    }
    features.push({type:'Feature',id:`C-${p.parentKey}`,
      geometry:{type:'MultiPolygon',coordinates:merged},
      properties:{...p,uid:`C-${p.parentKey}`,name:p.city || p.name,ward:'',
        officialCode: approximateParentCode(group),administrativeLevel:'designated-city',groupedCount:group.length}});
  }
  return {collection:{type:'FeatureCollection',features},groupCount:groups.size,unionFailures};
}

function approximateParentCode(features) {
  const first = features[0].properties.officialCode;
  // Explicit designated-city codes, source: https://www.jetro.go.jp/gov_procurement/list/local.html
  const designatedCities = {"札幌市": "01100", "仙台市": "04100", "さいたま市": "11100", "千葉市": "12100", "横浜市": "14100", "川崎市": "14130", "相模原市": "14150", "新潟市": "15100", "静岡市": "22100", "浜松市": "22130", "名古屋市": "23100", "京都市": "26100", "大阪市": "27100", "堺市": "27140", "神戸市": "28100", "岡山市": "33100", "広島市": "34100", "北九州市": "40100", "福岡市": "40130", "熊本市": "43100"};
  if(features.some(f=>f.properties.ward))return designatedCities[features[0].properties.city] || "";
  // Do not invent an authoritative parent-code from ward codes.
  if (features.length === 1) return first;
  // Retain codes for multipart pieces of the SAME administrative unit.
  const allCodes = features.map(f=>f.properties.officialCode);
  if (!features.some(f=>f.properties.ward) && allCodes.every(c=>c && c===first)) return first;
  return '';
}

export function estimateAreaKm2(feature) {
  // Spherical approximation; NOT an official statutory area. Radius in metres.
  const R2 = 6378137 ** 2;
  function ringArea(ring) {
    if (!ring || ring.length < 4) return 0;
    let sum = 0;
    for (let i=0, n=ring.length-1; i<n; i++) {
      const a=ring[i], b=ring[i+1];
      sum += (b[0]-a[0])*Math.PI/180 *
        (2+Math.sin(a[1]*Math.PI/180)+Math.sin(b[1]*Math.PI/180));
    }
    return Math.abs(sum * R2 / 2);
  }
  const polygons = toMultiCoordinates(feature.geometry);
  let area = 0;
  for (const poly of polygons) {
    area += ringArea(poly[0]);
    for(let i=1;i<poly.length;i++) area -= ringArea(poly[i]);
  }
  return Math.max(area/1e6,0);
}

export function bboxOfFeature(feature) {
  let bounds = [Infinity,Infinity,-Infinity,-Infinity];
  const walk = a => {
    if (!Array.isArray(a)) return;
    if (typeof a[0] === 'number' && typeof a[1] === 'number') {
      bounds[0] = Math.min(bounds[0],a[0]); bounds[1] = Math.min(bounds[1],a[1]);
      bounds[2] = Math.max(bounds[2],a[0]); bounds[3] = Math.max(bounds[3],a[1]);
    } else a.forEach(walk);
  };
  walk(feature.geometry.coordinates);
  return bounds.every(Number.isFinite) ? [[bounds[0],bounds[1]],[bounds[2],bounds[3]]] : null;
}

export function featureCollection(features=[]) {return {type:'FeatureCollection',features};}
