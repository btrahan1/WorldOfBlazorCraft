// WorldOfBlazorCraft - client-side 3D renderer (ES module)
// Loaded via: JS.InvokeAsync<IJSObjectReference>("import", "/js/game_renderer.js")
//
// THREE and GLTFLoader are loaded on demand the first time init() is called
// so there is no dependency on script load order or CDN timing.

/* globals THREE */

// ---------------------------------------------------------------------------
// THREE bootstrap - loads CDN scripts once and waits for them
// ---------------------------------------------------------------------------

function loadScript(src) {
    return new Promise((resolve, reject) => {
        if (document.querySelector(`script[src="${src}"]`)) {
            // Already injected - may still be loading; poll readyState via onload
            const existing = document.querySelector(`script[src="${src}"]`);
            if (existing.dataset.loaded) { resolve(); return; }
            existing.addEventListener('load',  () => resolve());
            existing.addEventListener('error', () => reject(new Error(`Script failed: ${src}`)));
            return;
        }
        const s = document.createElement('script');
        s.src = src;
        s.onload  = () => { s.dataset.loaded = '1'; resolve(); };
        s.onerror = () => reject(new Error(`Script failed: ${src}`));
        document.head.appendChild(s);
    });
}

async function ensureThree() {
    if (window.THREE && window.THREE.GLTFLoader && window.MeshoptDecoder) return;
    await loadScript('https://unpkg.com/three@0.147.0/build/three.min.js');
    await loadScript('https://unpkg.com/three@0.147.0/examples/js/loaders/GLTFLoader.js');
    await loadScript('https://unpkg.com/meshoptimizer@0.18.1/meshopt_decoder.js');
    if (window.MeshoptDecoder && window.MeshoptDecoder.ready) {
        await window.MeshoptDecoder.ready;
    }
}

// ---------------------------------------------------------------------------
// Terrain height - mirrors Terrain.cs exactly
// ---------------------------------------------------------------------------

const WATER_LEVEL = -4.5;

const ZONES = [
    { biome: 'vale',  zMin: -180, zMax:  180, hub: { x: 0, z:   0 } },
    { biome: 'marsh', zMin:  180, zMax:  540, hub: { x: 0, z: 300 } },
    { biome: 'peaks', zMin:  540, zMax:  900, hub: { x: 0, z: 660 } },
];

const BIOME_SHAPES = {
    vale:  { hill: 26.0, base:  0.0, hubHeight: 1.5 },
    marsh: { hill: 11.0, base: -1.0, hubHeight: 1.2 },
    peaks: { hill: 34.0, base:  7.0, hubHeight: 9.0 },
};

const CAMPS = [
    { x: -15, z:  55, r: 22 }, { x:  20, z:  70, r: 20 }, { x:  55, z:  12, r: 22 },
    { x:  80, z: -15, r: 18 }, { x: -60, z:   5, r: 22 }, { x: -82, z: -62, r: 20 },
    { x:  65, z: -65, r: 24 }, { x:  90, z: -90, r: 16 }, { x:  -8, z: 126, r: 22 },
    { x:  50, z: 124, r: 18 }, { x:  18, z: 150, r: 16 }, { x: -18, z: 142, r: 16 },
];

const LAKES = [
    { x:  -92, z:  88, r: 30 },
    { x: -110, z: 310, r: 35 }, { x: 60, z: 380, r: 25 }, { x: -40, z: 450, r: 20 },
    { x:  -70, z: 760, r: 18 },
];

function seededNoise(x, z, seed) {
    let n = Math.imul(Math.imul(Math.trunc(x * 73856093), Math.trunc(z * 19349663)) ^ seed, 1234567891);
    n = Math.imul(n ^ (n >>> 13), 1299709);
    return ((n >>> 0) / 4294967296.0) * 2.0 - 1.0;
}

function smoothNoise(x, z, seed) {
    const xi = Math.floor(x), zi = Math.floor(z);
    const fx = x - xi, fz = z - zi;
    const ux = fx * fx * (3.0 - 2.0 * fx), uz = fz * fz * (3.0 - 2.0 * fz);
    const n00 = seededNoise(xi,     zi,     seed);
    const n10 = seededNoise(xi + 1, zi,     seed);
    const n01 = seededNoise(xi,     zi + 1, seed);
    const n11 = seededNoise(xi + 1, zi + 1, seed);
    return n00 + (n10-n00)*ux + (n01-n00)*uz + (n00-n10-n01+n11)*ux*uz;
}

function multiOctave(x, z, seed, octaves, lacunarity, gain) {
    let v = 0, amp = 1, freq = 1, maxV = 0;
    for (let i = 0; i < octaves; i++) {
        v += smoothNoise(x * freq, z * freq, seed + i * 997) * amp;
        maxV += amp; amp *= gain; freq *= lacunarity;
    }
    return v / maxV;
}

function zoneBiomeBlend(z) {
    let biome = ZONES[0].biome, blend = ZONES[0].biome, t = 0;
    for (let i = 0; i + 1 < ZONES.length; i++) {
        const b = ZONES[i].zMax, raw = (z - (b - 30)) / 65;
        const tt = Math.max(0, Math.min(1, raw)), sm = tt * tt * (3 - 2 * tt);
        if (sm > 0) { biome = ZONES[i].biome; blend = ZONES[i+1].biome; t = sm; }
        if (z < b) break;
    }
    return { from: biome, to: blend, t };
}

function shapeAt(z) {
    const { from, to, t } = zoneBiomeBlend(z);
    const a = BIOME_SHAPES[from], b = BIOME_SHAPES[to];
    return { hill: a.hill+(b.hill-a.hill)*t, base: a.base+(b.base-a.base)*t, hubHeight: a.hubHeight+(b.hubHeight-a.hubHeight)*t };
}

function terrainHeight(x, z, seed) {
    const s = shapeAt(z);
    let hub = 0, lake = 0;
    for (const z2 of ZONES) { const d = Math.hypot(x-z2.hub.x, z-z2.hub.z); if (d<26) { const t=1-d/26; hub=Math.max(hub,t*t*(3-2*t)); } }
    for (const c of CAMPS)  { const d = Math.hypot(x-c.x, z-c.z);           if (d<c.r) { const t=1-d/c.r; hub=Math.max(hub,t*t*(3-2*t)*0.7); } }
    for (const l of LAKES)  { const d = Math.hypot(x-l.x, z-l.z);           if (d<l.r) { const t=1-d/l.r; lake=Math.max(lake,t*t*(3-2*t)); } }

    const macro  = multiOctave(x*0.013, z*0.013, seed,        6, 2, 0.50);
    const detail = multiOctave(x*0.05,  z*0.05,  seed+1000,   4, 2, 0.45);
    let h = s.base + macro * s.hill + detail * 2.5;

    // Ridge at zone 0 / zone 1 boundary
    const RH=22, RS=18, PHW=10, PS=34;
    const ridge = RH * Math.exp(-0.5 * ((z - ZONES[0].zMax) ** 2) / (RS*RS));
    const passT = Math.max(0, Math.min(1, (PHW - Math.abs(x)) / PS));
    h += ridge * (1 - passT*passT*(3-2*passT));

    // Crater
    const CX=149.5, CZ=295, CBR=20, CR=30, CD=2.6, CRH=0.95;
    const cd = Math.hypot(x-CX, z-CZ);
    if (cd < CR) {
        if (cd < CBR) { const t=cd/CBR; h += -CD*(1-t*t); }
        else           { h += CRH * Math.sin(((cd-CBR)/(CR-CBR)) * Math.PI); }
    }

    if (hub  > 0) h = h*(1-hub)  + s.hubHeight * hub;
    if (lake > 0) h = h*(1-lake) + (WATER_LEVEL-3) * lake;
    return h;
}

// ---------------------------------------------------------------------------
// Snapshot interpolation buffer
// ---------------------------------------------------------------------------

const INTERP_DELAY = 110; // ms
const snapshots    = [];

function pushSnapshot(snap) {
    snapshots.push({ t: performance.now(), d: snap });
    if (snapshots.length > 30) snapshots.shift();
}

function lerpAngle(a, b, t) {
    let d = b - a;
    while (d >  Math.PI) d -= 2*Math.PI;
    while (d < -Math.PI) d += 2*Math.PI;
    return a + d*t;
}

function interpolatedState() {
    const renderAt = performance.now() - INTERP_DELAY;
    let i = snapshots.length - 1;
    while (i > 0 && snapshots[i].t > renderAt) i--;
    if (i < 0) return null;
    const s0 = snapshots[i], s1 = snapshots[i+1] || null;
    if (!s1) return s0.d;
    const t = Math.min(1, (renderAt - s0.t) / Math.max(1, s1.t - s0.t));
    function ie(a, b) { return { id:a.id, k:a.k, tid:a.tid, nm:a.nm, lv:a.lv, x:a.x+(b.x-a.x)*t, y:a.y+(b.y-a.y)*t, z:a.z+(b.z-a.z)*t, f:lerpAngle(a.f,b.f,t), hp:a.hp, mhp:a.mhp }; }
    const self = (s0.d.self && s1.d.self) ? ie(s0.d.self, s1.d.self) : s0.d.self || null;
    const ents = [];
    for (const e0 of (s0.d.ents || [])) { const e1=(s1.d.ents||[]).find(e=>e.id===e0.id); ents.push(e1?ie(e0,e1):e0); }
    return { self, ents };
}

// ---------------------------------------------------------------------------
// Renderer state
// ---------------------------------------------------------------------------

let renderer=null, scene=null, camera=null, clock=null, animId=null;
const cam = { yaw:Math.PI, pitch:0.4, dist:14, minPitch:0.08, maxPitch:1.45, minDist:2, maxDist:45, dragging:false, lastX:0, lastY:0 };
const views = new Map(), modelCache = new Map(), modelLoadQ = new Map();
let selfPos = { x:0, y:0, z:0, f:0 }, worldSeed = 42;

// ---------------------------------------------------------------------------
// Model loading (uses window.THREE.GLTFLoader global)
// ---------------------------------------------------------------------------

const CREATURE_MAP = {
    forest_wolf:'wolf', wild_boar:'wild_boar', webwood_spider:'spider',
    tunnel_rat:'goblin', vale_bandit:'orc', restless_bones:'ghost',
    fen_troll:'orc', goblin:'goblin', orc:'orc',
};

function modelUrl(ent) {
    return ent.k === 'player'
        ? '/models/chars/players/mage.glb'
        : `/models/creatures/${CREATURE_MAP[ent.tid] || 'goblin'}.glb`;
}

let gltfLoader = null;
function getLoader() {
    if (!gltfLoader) {
        gltfLoader = new THREE.GLTFLoader();
        if (window.MeshoptDecoder) {
            gltfLoader.setMeshoptDecoder(window.MeshoptDecoder);
        }
    }
    return gltfLoader;
}

function loadModel(url) {
    if (modelCache.has(url)) return Promise.resolve(modelCache.get(url));
    if (modelLoadQ.has(url)) return modelLoadQ.get(url);
    const p = new Promise((resolve, reject) => {
        getLoader().load(url, g => { modelCache.set(url, g); resolve(g); }, undefined, reject);
    });
    modelLoadQ.set(url, p);
    return p;
}

// ---------------------------------------------------------------------------
// Entity view management
// ---------------------------------------------------------------------------

function ensureView(ent) {
    if (views.has(ent.id)) return;
    const group = new THREE.Group();
    group.position.set(ent.x, ent.y, -ent.z);
    scene.add(group);
    const view = { group, mixer:null, clips:{}, lastAnim:null };
    views.set(ent.id, view);
    // Placeholder capsule while model loads
    const ph = new THREE.Mesh(
        new THREE.CapsuleGeometry(0.3, 1.0, 4, 8),
        new THREE.MeshLambertMaterial({ color: ent.k==='player' ? 0x4499ff : 0xff4422 })
    );
    ph.position.set(0, 0.8, 0); ph.userData.ph = true;
    group.add(ph);

    loadModel(modelUrl(ent)).then(gltf => {
        const v = views.get(ent.id); if (!v) return;
        const p = v.group.children.find(c => c.userData.ph);
        if (p) v.group.remove(p);
        const model = gltf.scene.clone(true);
        v.group.add(model);
        if (gltf.animations?.length) {
            v.mixer = new THREE.AnimationMixer(model);
            for (const clip of gltf.animations) v.clips[clip.name.toLowerCase()] = clip;
            playAnim(v, 'idle');
        }
    }).catch(() => { /* placeholder stays */ });
}

function playAnim(view, name) {
    if (!view.mixer) return;
    const key = Object.keys(view.clips).find(k => k.includes(name));
    if (!key || view.lastAnim === key) return;
    view.mixer.stopAllAction();
    view.mixer.clipAction(view.clips[key]).reset().setLoop(THREE.LoopRepeat, Infinity).play();
    view.lastAnim = key;
}

function removeView(id) {
    const v = views.get(id); if (!v) return;
    scene.remove(v.group);
    if (v.mixer) v.mixer.stopAllAction();
    views.delete(id);
}

// ---------------------------------------------------------------------------
// Roads & seeded noise for decorations (mirrors world.ts / rng.ts)
// ---------------------------------------------------------------------------

const ROADS = [
  [{ x: 0, z: 8 }, { x: -8, z: 30 }, { x: -15, z: 55 }, { x: -2, z: 78 }],
  [{ x: 8, z: 2 }, { x: 30, z: 8 }, { x: 55, z: 12 }],
  [{ x: 6, z: -6 }, { x: 30, z: -30 }, { x: 50, z: -50 }, { x: 65, z: -65 }],
  [{ x: -8, z: 6 }, { x: -35, z: 25 }, { x: -58, z: 48 }, { x: -66, z: 58 }],
  [{ x: -6, z: -6 }, { x: -30, z: -28 }, { x: -55, z: -45 }, { x: -70, z: -55 }],
  [{ x: 6, z: 8 }, { x: 35, z: 35 }, { x: 60, z: 60 }, { x: 78, z: 74 }],
];

function hash2(x, y, seed) {
  let h = seed >>> 0;
  h = Math.imul(h ^ (x * 374761393), 668265263);
  h = Math.imul(h ^ (y * 1274126177), 461845907);
  h ^= h >>> 13;
  h = Math.imul(h, 1274126177);
  h ^= h >>> 16;
  return (h >>> 0) / 4294967296;
}

function roadDistance(x, z) {
  let best = Infinity;
  for (const road of ROADS) {
    for (let i = 0; i < road.length - 1; i++) {
      const a = road[i], b = road[i + 1];
      const abx = b.x - a.x, abz = b.z - a.z;
      const apx = x - a.x, apz = z - a.z;
      const len2 = abx * abx + abz * abz;
      const t = len2 > 0 ? Math.max(0, Math.min(1, (apx * abx + apz * abz) / len2)) : 0;
      const dx = apx - abx * t, dz = apz - abz * t;
      const d = Math.hypot(dx, dz);
      if (d < best) best = d;
    }
  }
  return best;
}

function generateDecorations(seed) {
  const out = [];
  const step = 10;
  const xHalf = 180 - 14;
  for (let gx = -xHalf; gx < xHalf; gx += step) {
    for (let gz = -180 + 14; gz < 180 - 14; gz += step) {
      const r = hash2(Math.round(gx), Math.round(gz), seed + 31);
      const biome = 'vale';
      let kind = null;
      if (r > 0.48) continue;
      kind = r < 0.30 ? 'tree' : r < 0.40 ? 'tree2' : 'rock';
      
      const ox = (hash2(Math.round(gx), Math.round(gz), seed + 57) - 0.5) * step;
      const oz = (hash2(Math.round(gx), Math.round(gz), seed + 91) - 0.5) * step;
      const x = gx + ox, z = gz + oz;
      
      let inHub = false;
      const hubX = 0, hubZ = 0, hubR = 26;
      if (Math.hypot(x - hubX, z - hubZ) < hubR + 4) inHub = true;
      if (inHub) continue;
      
      if (terrainHeight(x, z, seed) < WATER_LEVEL + 1) continue;
      if (roadDistance(x, z) < 5) continue;
      
      let inCamp = false;
      for (const c of CAMPS) {
        if (Math.hypot(x - c.x, z - c.z) < c.r + 3) { inCamp = true; break; }
      }
      if (inCamp) continue;
      
      out.push({
        kind,
        x, z,
        scale: 0.7 + hash2(Math.round(gx), Math.round(gz), seed + 13) * 0.9,
        variant: Math.floor(hash2(Math.round(gx), Math.round(gz), seed + 77) * 3),
        biome,
      });
    }
  }
  return out;
}

// ---------------------------------------------------------------------------
// Terrain mesh (Eastbrook Vale, zone 1)
// ---------------------------------------------------------------------------

function buildTerrain(seed) {
    const X0=-180, X1=180, Z0=-180, Z1=180, STEP=6;
    const nx = Math.ceil((X1-X0)/STEP), nz = Math.ceil((Z1-Z0)/STEP);
    const N = (nx+1)*(nz+1);
    const pos = new Float32Array(N*3), nor = new Float32Array(N*3), col = new Float32Array(N*3);
    const GRASS=new THREE.Color(0x548545), SAND=new THREE.Color(0xc2b283),
          ROCK =new THREE.Color(0x7a7a72), SNOW=new THREE.Color(0xedf3fa), tmp=new THREE.Color();
    for (let j=0; j<=nz; j++) {
        for (let i=0; i<=nx; i++) {
            const wx=X0+i*STEP, wz=Z0+j*STEP, h=terrainHeight(wx,wz,seed), vi=(j*(nx+1)+i);
            pos[vi*3]=wx; pos[vi*3+1]=h; pos[vi*3+2]=wz;
            tmp.copy(GRASS);
            tmp.lerp(SAND, Math.max(0,Math.min(1,(WATER_LEVEL+1.8-h)/1.8)));
            
            // Paint dirt roads onto the terrain
            const rd = roadDistance(wx, wz);
            if (rd < 5) {
                const roadBlend = Math.max(0, Math.min(1, (5 - rd) / 1.5));
                const ROAD_COLOR = new THREE.Color(0x8a6f47);
                tmp.lerp(ROAD_COLOR, roadBlend);
            }
            if (h>22) tmp.lerp(ROCK, Math.min(1,(h-22)/12));
            if (h>34) tmp.lerp(SNOW, Math.min(1,(h-34)/10));
            col[vi*3]=tmp.r; col[vi*3+1]=tmp.g; col[vi*3+2]=tmp.b;
        }
    }
    for (let j=0; j<=nz; j++) {
        for (let i=0; i<=nx; i++) {
            const vi=j*(nx+1)+i, wx=X0+i*STEP, wz=Z0+j*STEP;
            const dx=(terrainHeight(wx+1,wz,seed)-terrainHeight(wx-1,wz,seed))/2;
            const dz=(terrainHeight(wx,wz+1,seed)-terrainHeight(wx,wz-1,seed))/2;
            const len=Math.hypot(dx,1,dz);
            nor[vi*3]=-dx/len; nor[vi*3+1]=1/len; nor[vi*3+2]=-dz/len;
        }
    }
    const idx = new Uint32Array(nx*nz*6); let k=0;
    for (let j=0; j<nz; j++) for (let i=0; i<nx; i++) {
        const a=j*(nx+1)+i, b=a+1, c=a+(nx+1), d=c+1;
        idx[k++]=a; idx[k++]=c; idx[k++]=b; idx[k++]=b; idx[k++]=c; idx[k++]=d;
    }
    const geo = new THREE.BufferGeometry();
    geo.setAttribute('position', new THREE.BufferAttribute(pos, 3));
    geo.setAttribute('normal',   new THREE.BufferAttribute(nor, 3));
    geo.setAttribute('color',    new THREE.BufferAttribute(col, 3));
    geo.setIndex(new THREE.BufferAttribute(idx, 1));
    const mesh = new THREE.Mesh(geo, new THREE.MeshLambertMaterial({ vertexColors: true }));
    mesh.receiveShadow = true;
    return mesh;
}

function buildWater() {
    const mesh = new THREE.Mesh(
        new THREE.PlaneGeometry(360, 720, 1, 1),
        new THREE.MeshLambertMaterial({ color: 0x2255aa, transparent: true, opacity: 0.72 })
    );
    mesh.rotation.x = -Math.PI/2;
    mesh.position.set(0, WATER_LEVEL+0.05, -180);
    return mesh;
}

// ---------------------------------------------------------------------------
// World Decorations & Foliage (pine, oak, rock, static town props)
// ---------------------------------------------------------------------------

function spawnInstanceGroup(gltf, positions, scaleMult, yOffset) {
    gltf.scene.updateMatrixWorld(true);
    const meshes = [];
    gltf.scene.traverse(child => {
        if (child.isMesh) meshes.push(child);
    });
    
    const instancedMeshes = [];
    for (const m of meshes) {
        const geo = m.geometry.clone();
        geo.applyMatrix4(m.matrixWorld);
        const im = new THREE.InstancedMesh(geo, m.material, positions.length);
        im.castShadow = true;
        im.receiveShadow = true;
        instancedMeshes.push(im);
    }
    
    const tempMatrix = new THREE.Matrix4();
    const tempPosition = new THREE.Vector3();
    const tempRotation = new THREE.Quaternion();
    const tempScale = new THREE.Vector3();
    
    positions.forEach((pos, idx) => {
        tempPosition.set(pos.x, pos.y + (yOffset || 0), -pos.z);
        tempRotation.setFromEuler(new THREE.Euler(0, pos.rot, 0));
        const s = pos.scale * scaleMult;
        tempScale.set(s, s, s);
        tempMatrix.compose(tempPosition, tempRotation, tempScale);
        
        for (const mesh of instancedMeshes) {
            mesh.setMatrixAt(idx, tempMatrix);
        }
    });
    
    for (const mesh of instancedMeshes) {
        mesh.instanceMatrix.needsUpdate = true;
        scene.add(mesh);
    }
}

function spawnProp(gltf, x, y, z, rot, scale) {
    const model = gltf.scene.clone(true);
    model.position.set(x, y, -z);
    model.rotation.y = rot;
    if (scale !== undefined) {
        if (typeof scale === 'number') model.scale.set(scale, scale, scale);
        else model.scale.set(scale[0], scale[1], scale[2]);
    }
    model.traverse(child => {
        if (child.isMesh) {
            child.castShadow = true;
            child.receiveShadow = true;
        }
    });
    scene.add(model);
}

async function buildWorldDecorations(seed) {
    // 1. Spawning Foliage (Trees & Rocks)
    const pines = [];
    const oaks = [];
    const rocks = [];
    
    const decos = generateDecorations(seed);
    for (const d of decos) {
        const y = terrainHeight(d.x, d.z, seed);
        const rot = hash2(Math.round(d.x), Math.round(d.z), seed + 11) * Math.PI * 2;
        const item = { x: d.x, y, z: d.z, rot, scale: d.scale };
        if (d.kind === 'tree') pines.push(item);
        else if (d.kind === 'tree2') oaks.push(item);
        else if (d.kind === 'rock') rocks.push(item);
    }
    
    const [pineGLTF, oakGLTF, rockGLTF] = await Promise.all([
        loadModel('/models/foliage/pine_1.glb'),
        loadModel('/models/foliage/oak_1.glb'),
        loadModel('/models/foliage/rock_1.glb')
    ]);
    
    spawnInstanceGroup(pineGLTF, pines, 1.1, -0.05);
    spawnInstanceGroup(oakGLTF, oaks, 1.15, -0.05);
    spawnInstanceGroup(rockGLTF, rocks, 1.0, 0);
    
    // 2. Spawning Static Props
    const propModels = {};
    const propUrls = {
        house1: '/models/props/house_1.glb',
        house2: '/models/props/house_2.glb',
        inn: '/models/props/inn.glb',
        chapel: '/models/props/house_3.glb',
        well: '/models/props/well.glb',
        stand1: '/models/props/market_stand_1.glb',
        stand2: '/models/props/market_stand_2.glb',
        tent: '/models/props/tent_small.glb',
        crate: '/models/props/crate_wooden.glb',
        bonfire: '/models/props/bonfire.glb',
        mudHut: '/models/props/mushroom_red.glb',
        column: '/models/props/column.glb',
        columnBroken: '/models/props/column_broken.glb',
        dock: '/models/props/dock_platform.glb',
        fence: '/models/props/fence.glb'
    };
    
    await Promise.all(Object.entries(propUrls).map(async ([key, url]) => {
        propModels[key] = await loadModel(url);
    }));
    
    // Spawn houses
    spawnProp(propModels.house1, 10, terrainHeight(10, 12, seed) - 0.12, 12, -0.4, [1.0, 1.0, 1.0]);
    spawnProp(propModels.house2, -10, terrainHeight(-10, 10, seed) - 0.12, 10, 0.5, [1.0, 1.0, 1.0]);
    spawnProp(propModels.inn, 12, terrainHeight(12, -6, seed) - 0.12, -6, 2.4, [1.0, 1.0, 1.0]);
    spawnProp(propModels.chapel, -16, terrainHeight(-16, -8, seed) - 0.12, -8, 0.9, [1.0, 1.0, 1.0]);
    
    // Spawn wells
    spawnProp(propModels.well, 0, terrainHeight(0, 2, seed) - 0.1, 2, 0, [1.0, 1.0, 1.0]);
    
    // Spawn stalls
    spawnProp(propModels.stand1, -8.5, terrainHeight(-8.5, 3, seed) - 0.06, 3, Math.PI / 2, [1.0, 1.0, 1.0]);
    spawnProp(propModels.stand2, 9.5, terrainHeight(9.5, 17.5, seed) - 0.06, 17.5, -2.7, [1.0, 1.0, 1.0]);
    spawnProp(propModels.stand1, 0, terrainHeight(0, 11.5, seed) - 0.06, 11.5, Math.PI, [1.1, 1.1, 1.1]);
    
    // Spawn tents
    spawnProp(propModels.tent, 62, terrainHeight(62, -61, seed) - 0.06, -61, 0.4, 1.0);
    spawnProp(propModels.tent, 69, terrainHeight(69, -69, seed) - 0.06, -69, 2.1, 1.0);
    spawnProp(propModels.tent, 88, terrainHeight(88, -86, seed) - 0.06, -86, 1.2, 1.3);
    spawnProp(propModels.tent, 95, terrainHeight(95, -94, seed) - 0.06, -94, -0.6, 1.0);
    
    // Spawn crates
    const crates = [[60, -63], [66, -67], [87, -88], [93, -90], [70, -72]];
    crates.forEach(([x, z]) => {
        spawnProp(propModels.crate, x, terrainHeight(x, z, seed) - 0.04, z, 0, 1.2);
    });
    
    // Spawn campfires (bonfires)
    const campfires = [[3, -4], [65, -65], [90, -90], [-80, -60], [-61, 56]];
    campfires.forEach(([x, z]) => {
        const y = terrainHeight(x, z, seed);
        spawnProp(propModels.bonfire, x, y - 0.05, z, 0, 4.3);
        
        // Add campfire light
        const light = new THREE.PointLight(0xff8830, 8, 12, 1.5);
        light.position.set(x, y + 1.0, -z);
        scene.add(light);
    });
    
    // Spawn mud huts (Murloc camp)
    const mudHuts = [[-73, 59], [-78, 54], [-69, 55]];
    mudHuts.forEach(([x, z]) => {
        spawnProp(propModels.mudHut, x, terrainHeight(x, z, seed) - 0.15, z, 0, [13, 10.5, 13]);
    });
    
    // Spawn ruin rings
    const ring = { x: 80, z: 78, ringR: 7, columns: 7 };
    for (let i = 0; i < ring.columns; i++) {
        const ang = (i / ring.columns) * Math.PI * 2;
        const x = ring.x + Math.sin(ang) * ring.ringR;
        const z = ring.z + Math.cos(ang) * ring.ringR;
        const intact = i % 4 === 1;
        const kind = intact ? propModels.column : propModels.columnBroken;
        const sy = intact ? 3.5 + (i % 2) * 0.5 : 1.7 + (i % 3) * 0.85;
        spawnProp(kind, x, terrainHeight(x, z, seed) - 0.1, z, 0, [3.8, sy, 3.8]);
    }
    
    // Spawn docks
    spawnProp(propModels.dock, -64, terrainHeight(-64, 60, seed) + 0.15, 60, -2.2, [0.78, 0.52, 0.85]);
    
    // Spawn fences
    const fences = [
        { x1: 16, z1: 16, x2: 22, z2: 4 },
        { x1: -16, z1: 14, x2: -20, z2: 2 },
    ];
    fences.forEach(f => {
        const len = Math.hypot(f.x2 - f.x1, f.z2 - f.z1);
        const dirx = (f.x2 - f.x1) / len, dirz = (f.z2 - f.z1) / len;
        const yaw = Math.atan2(-dirz, dirx);
        const mx = (f.x1 + f.x2) / 2, mz = (f.z1 + f.z2) / 2;
        const my = terrainHeight(mx, mz, seed) - 0.05;
        spawnProp(propModels.fence, mx, my, mz, yaw, [3.0, 2.9, 3.0]);
    });
}

// ---------------------------------------------------------------------------
// Camera
// ---------------------------------------------------------------------------

function updateCamera() {
    const tgt = new THREE.Vector3(selfPos.x, selfPos.y+1.5, -selfPos.z);
    camera.position.set(
        tgt.x + cam.dist * Math.sin(cam.yaw)   * Math.cos(cam.pitch),
        tgt.y + cam.dist * Math.sin(cam.pitch),
        tgt.z + cam.dist * Math.cos(cam.yaw)   * Math.cos(cam.pitch)
    );
    camera.lookAt(tgt);
}

// ---------------------------------------------------------------------------
// Render loop
// ---------------------------------------------------------------------------

function tick() {
    animId = requestAnimationFrame(tick);
    const dt = clock.getDelta();
    const state = interpolatedState();
    if (state) {
        if (state.self) selfPos = state.self;
        const activeIds = new Set();
        const all = state.self ? [state.self, ...(state.ents||[])] : (state.ents||[]);
        for (const ent of all) {
            activeIds.add(ent.id);
            ensureView(ent);
            const v = views.get(ent.id); if (!v) continue;
            v.group.position.set(ent.x, ent.y, -ent.z);
            v.group.rotation.y = -ent.f;
            if (v.mixer) {
                const px=v.group.userData.px??ent.x, pz=v.group.userData.pz??ent.z;
                const spd=Math.hypot(ent.x-px, ent.z-pz)/Math.max(dt,0.001);
                v.group.userData.px=ent.x; v.group.userData.pz=ent.z;
                playAnim(v, spd>0.5?'walk':'idle');
                v.mixer.update(dt);
            }
        }
        for (const [id] of views) { if (!activeIds.has(id)) removeView(id); }
    }
    updateCamera();
    renderer.render(scene, camera);
}

// ---------------------------------------------------------------------------
// Mouse / wheel handlers
// ---------------------------------------------------------------------------

function onDown(e)  { if (e.button===2||e.button===0) { cam.dragging=true; cam.lastX=e.clientX; cam.lastY=e.clientY; } }
function onMove(e)  { if (!cam.dragging) return; cam.yaw-=(e.clientX-cam.lastX)*0.005; cam.pitch=Math.max(cam.minPitch,Math.min(cam.maxPitch,cam.pitch+(e.clientY-cam.lastY)*0.005)); cam.lastX=e.clientX; cam.lastY=e.clientY; }
function onUp()     { cam.dragging=false; }
function onWheel(e) { cam.dist=Math.max(cam.minDist,Math.min(cam.maxDist,cam.dist+e.deltaY*0.02)); }
function onCtx(e)   { e.preventDefault(); }

// ---------------------------------------------------------------------------
// Public API - exported ES module functions called via IJSObjectReference
// ---------------------------------------------------------------------------

export async function init(canvas, seed) {
    if (renderer) return;
    await ensureThree(); // wait for window.THREE and window.THREE.GLTFLoader to be ready
    worldSeed = seed || 42;

    renderer = new THREE.WebGLRenderer({ canvas, antialias: true, powerPreference: 'high-performance' });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    renderer.setSize(canvas.clientWidth, canvas.clientHeight, false);
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.1;

    clock  = new THREE.Clock();
    scene  = new THREE.Scene();
    scene.background = new THREE.Color(0x7ab4d0);
    scene.fog = new THREE.Fog(0x7ab4d0, 120, 400);

    camera = new THREE.PerspectiveCamera(60, canvas.clientWidth / canvas.clientHeight, 0.1, 800);

    scene.add(new THREE.HemisphereLight(0xdcefff, 0x465f39, 1.2));
    const sun = new THREE.DirectionalLight(0xffedd0, 2.2);
    sun.position.set(80, 120, -60);
    sun.castShadow = true;
    sun.shadow.camera.left = sun.shadow.camera.bottom = -200;
    sun.shadow.camera.right = sun.shadow.camera.top = 200;
    sun.shadow.camera.far = 800;
    sun.shadow.mapSize.set(2048, 2048);
    scene.add(sun);

    scene.add(buildTerrain(worldSeed));
    scene.add(buildWater());
    
    // Asynchronously load and spawn static props, trees, and rocks
    buildWorldDecorations(worldSeed).catch(e => console.error("Error loading decorations:", e));

    canvas.addEventListener('mousedown',   onDown);
    canvas.addEventListener('mousemove',   onMove);
    canvas.addEventListener('mouseup',     onUp);
    canvas.addEventListener('wheel',       onWheel, { passive: true });
    canvas.addEventListener('contextmenu', onCtx);

    new ResizeObserver(() => {
        if (!renderer) return;
        renderer.setSize(canvas.clientWidth, canvas.clientHeight, false);
        camera.aspect = canvas.clientWidth / canvas.clientHeight;
        camera.updateProjectionMatrix();
    }).observe(canvas);

    updateCamera();
    tick();
}

export function updateState(snap) {
    if (!scene || !snap) return;
    pushSnapshot(snap);
}

export function resize() {
    if (!renderer || !camera) return;
    const c = renderer.domElement;
    renderer.setSize(c.clientWidth, c.clientHeight, false);
    camera.aspect = c.clientWidth / c.clientHeight;
    camera.updateProjectionMatrix();
}

export function dispose() {
    if (animId) cancelAnimationFrame(animId);
    animId = null;
    if (renderer) renderer.dispose();
    renderer = null; scene = null; camera = null;
    views.clear(); snapshots.length = 0;
}
