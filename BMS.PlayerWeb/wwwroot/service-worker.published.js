// Service worker của PlayerWeb (bản publish).
// - CHỈ cache file tĩnh của chính trang (khung Blazor, CSS, ảnh, icon) theo danh sách
//   service-worker-assets.js do .NET sinh ra lúc publish (kèm mã băm kiểm tra toàn vẹn).
// - KHÔNG cache API (api.<tên miền>), SignalR (/hubs/bms), appsettings.json hay dữ liệu cá nhân:
//   mọi yêu cầu khác tên miền / không phải GET đi thẳng ra mạng.
// - Mỗi lần triển khai, danh sách tài nguyên có "version" mới -> cache mới, cache cũ bị xóa;
//   trang hiện thông báo "Đã có phiên bản mới" để người dùng tải lại (xem index.html).
// - Mất mạng khi mở trang -> trả về offline.html (có nút Thử lại).
self.importScripts('./service-worker-assets.js');

const cacheNamePrefix = 'bms-offline-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlinePage = 'offline.html';
const offlineAssetsInclude = [/\.dll$/, /\.pdb$/, /\.wasm/, /\.html$/, /\.js$/, /\.css$/, /\.woff2?$/, /\.png$/, /\.jpe?g$/,
    /\.gif$/, /\.ico$/, /\.svg$/, /\.webp$/, /\.dat$/, /\.blat$/, /\.webmanifest$/];
const offlineAssetsExclude = [/^service-worker\.js$/, /^appsettings(\..+)?\.json$/];

self.addEventListener('install', event => event.waitUntil(onInstall()));
self.addEventListener('activate', event => event.waitUntil(onActivate()));
self.addEventListener('fetch', event => onFetch(event));
self.addEventListener('message', event => {
    if (event.data === 'SKIP_WAITING') {
        self.skipWaiting();
    }
});

async function onInstall() {
    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));
    const cache = await caches.open(cacheName);
    await cache.addAll(assetsRequests);
}

async function onActivate() {
    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map(key => caches.delete(key)));
    await self.clients.claim();
}

function onFetch(event) {
    const request = event.request;
    const url = new URL(request.url);

    // API, SignalR và mọi thứ khác tên miền / không phải GET: không can thiệp.
    if (request.method !== 'GET' || url.origin !== self.location.origin) {
        return;
    }
    // Cấu hình luôn lấy mới từ máy chủ.
    if (offlineAssetsExclude.some(pattern => pattern.test(url.pathname.substring(1)))) {
        return;
    }

    // Mở trang (/, /cues, /wallet...): ưu tiên mạng để luôn nhận index.html mới nhất;
    // mất mạng thì hiện trang offline.
    if (request.mode === 'navigate') {
        event.respondWith(fetch(request).catch(async () =>
            (await caches.match(offlinePage, { cacheName })) ?? Response.error()));
        return;
    }

    // File tĩnh: lấy trong cache trước, chưa có thì tải từ mạng.
    event.respondWith((async () => (await caches.match(request, { cacheName })) ?? fetch(request))());
}
