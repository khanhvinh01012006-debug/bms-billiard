// Bản dùng khi chạy Development: KHÔNG cache gì để luôn thấy code mới nhất.
// Bản thật khi publish là service-worker.published.js (xem BMS.PlayerWeb.csproj).
self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', event => event.waitUntil(self.clients.claim()));
self.addEventListener('fetch', () => { });
