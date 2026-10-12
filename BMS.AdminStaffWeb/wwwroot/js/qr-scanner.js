// Quét mã QR hội viên ở cổng nhân viên.
// - Camera sau (getUserMedia, cần HTTPS). Chrome/Edge Android có BarcodeDetector sẵn; trình duyệt
//   không có (Safari iOS, Firefox) thì tự tải jsQR (wwwroot/lib/jsqr, Apache-2.0) khi cần.
// - Dự phòng: chụp/chọn ảnh có mã QR (decodeFile), hoặc nhân viên dán mã bằng tay (phía Blazor).
window.bmsQrScanner = (() => {
    let stream = null, timer = null, video = null, detector = null, dotnet = null;
    const canvas = document.createElement('canvas');
    const ctx = canvas.getContext('2d', { willReadFrequently: true });

    const loadJsQr = () => window.jsQR ? Promise.resolve() : new Promise((resolve, reject) => {
        const s = document.createElement('script');
        s.src = 'lib/jsqr/jsQR.js';
        s.onload = resolve;
        s.onerror = reject;
        document.head.appendChild(s);
    });

    async function ensureDecoder() {
        if (!detector && 'BarcodeDetector' in window) {
            try {
                const formats = await window.BarcodeDetector.getSupportedFormats();
                if (formats.includes('qr_code')) detector = new window.BarcodeDetector({ formats: ['qr_code'] });
            } catch { /* trình duyệt có API nhưng không hỗ trợ QR */ }
        }
        if (!detector) await loadJsQr();
    }

    async function decode(source, width, height) {
        if (detector) {
            try {
                const found = await detector.detect(source);
                if (found.length) return found[0].rawValue;
            } catch { /* thử jsQR bên dưới */ }
        }
        if (!window.jsQR) await loadJsQr();
        canvas.width = width;
        canvas.height = height;
        ctx.drawImage(source, 0, 0, width, height);
        const image = ctx.getImageData(0, 0, width, height);
        return window.jsQR(image.data, width, height, { inversionAttempts: 'attemptBoth' })?.data ?? null;
    }

    // Mở camera và quét liên tục; đọc được mã -> gọi .NET OnQrScanned(text). Trả về thông báo lỗi
    // (tiếng Việt) nếu không mở được camera, null nếu đang quét.
    async function start(videoElement, dotnetRef) {
        stop();
        dotnet = dotnetRef;
        video = videoElement;
        await ensureDecoder();
        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
            return 'Trình duyệt này không mở được camera. Hãy chụp ảnh mã QR hoặc dán mã bên dưới.';
        }
        try {
            stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' }, audio: false });
        } catch (e) {
            return e && e.name === 'NotAllowedError'
                ? 'Chưa cho phép dùng camera. Hãy cho phép camera trong cài đặt trình duyệt, hoặc chụp ảnh mã QR.'
                : 'Không mở được camera. Hãy chụp ảnh mã QR hoặc dán mã bên dưới.';
        }
        video.srcObject = stream;
        video.setAttribute('playsinline', '');
        video.muted = true;
        await video.play();

        const tick = async () => {
            if (!stream) return;
            if (video.readyState >= 2 && video.videoWidth > 0) {
                const text = await decode(video, video.videoWidth, video.videoHeight);
                if (text) {
                    stop();
                    await dotnet.invokeMethodAsync('OnQrScanned', text);
                    return;
                }
            }
            timer = setTimeout(tick, 250);
        };
        tick();
        return null;
    }

    function stop() {
        clearTimeout(timer);
        timer = null;
        if (stream) stream.getTracks().forEach(t => t.stop());
        stream = null;
        if (video) video.srcObject = null;
    }

    // Đọc mã QR từ ảnh người dùng chụp/chọn (input type=file). Trả về chuỗi hoặc null.
    async function decodeFile(inputElement) {
        const file = inputElement.files && inputElement.files[0];
        if (!file) return null;
        await ensureDecoder();
        const bitmap = await createImageBitmap(file);
        const scale = Math.min(1, 1280 / Math.max(bitmap.width, bitmap.height));
        const width = Math.round(bitmap.width * scale), height = Math.round(bitmap.height * scale);
        const text = await decode(scale === 1 ? bitmap : await createImageBitmap(file, { resizeWidth: width, resizeHeight: height }), width, height);
        inputElement.value = '';
        return text;
    }

    return { start, stop, decodeFile };
})();
