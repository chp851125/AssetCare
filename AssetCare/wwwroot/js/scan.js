// 掃碼頁：相機掃描、照片辨識；掃到內容後只取資產編號送回本站查詢，不直接開啟掃到的網址。
(() => {
    const message = document.getElementById('scanMessage');
    const startButton = document.getElementById('startScan');
    const stopButton = document.getElementById('stopScan');
    const fileInput = document.getElementById('scanFile');
    const form = document.getElementById('manualForm');
    // 瀏覽器有內建條碼辨識時優先使用，辨識率較高。
    const options = { experimentalFeatures: { useBarCodeDetectorIfSupported: true } };
    let scanner = null;
    let done = false;

    const show = text => {
        message.textContent = text;
        message.classList.toggle('d-none', !text);
    };

    // 標籤內容為 .../Assets/ByNo/{編號}；也接受只有編號的條碼。
    const assetNoOf = text => {
        const fromUrl = /\/Assets\/ByNo\/([^/?#\s]+)/i.exec(text);
        if (fromUrl) {
            try { return decodeURIComponent(fromUrl[1]); } catch { return null; }
        }
        const plain = text.trim();
        return /^AST-[A-Z0-9]{2,6}-\d{4,8}$/i.test(plain) ? plain : null;
    };

    const handle = text => {
        if (done) return;
        const assetNo = assetNoOf(text);
        if (!assetNo) {
            show('這不是本系統的設備標籤，請掃描資產上的 QR Code。');
            return;
        }
        done = true;
        form.elements.code.value = assetNo;
        form.submit();
    };

    const toggle = scanning => {
        startButton.classList.toggle('d-none', scanning);
        stopButton.classList.toggle('d-none', !scanning);
    };

    const stop = async () => {
        if (scanner?.isScanning) await scanner.stop();
        scanner?.clear();
        scanner = null;
        toggle(false);
    };

    const cameraProblem = () => {
        if (!window.isSecureContext)
            return '目前不是安全連線（HTTPS），瀏覽器不允許開啟相機。請改用「拍照或選擇照片辨識」或手動輸入資產編號；也可改用 HTTPS 網址開啟本系統。';
        if (!navigator.mediaDevices?.getUserMedia)
            return '此瀏覽器不支援相機掃描，請改用「拍照或選擇照片辨識」或手動輸入資產編號。';
        return null;
    };

    if (typeof Html5Qrcode === 'undefined') {
        show('掃描元件載入失敗，請手動輸入資產編號。');
        startButton.disabled = true;
        fileInput.disabled = true;
        return;
    }

    const problem = cameraProblem();
    if (problem) {
        show(problem);
        startButton.disabled = true;
    }

    startButton.addEventListener('click', async () => {
        show('');
        done = false;
        startButton.disabled = true;
        try {
            scanner = new Html5Qrcode('reader', options);
            await scanner.start(
                { facingMode: 'environment' },
                { fps: 10, qrbox: (width, height) => { const side = Math.floor(Math.min(width, height) * 0.7); return { width: side, height: side }; } },
                handle);
            toggle(true);
        } catch {
            await stop();
            show('無法開啟相機，請確認已允許相機權限，或改用照片辨識、手動輸入。');
        } finally {
            startButton.disabled = false;
        }
    });

    stopButton.addEventListener('click', stop);
    window.addEventListener('pagehide', () => { stop(); });

    // 辨識元件對高解析度照片常常失敗，因此依序縮成不同尺寸重試。
    const resized = async (bitmap, longSide) => {
        const scale = longSide / Math.max(bitmap.width, bitmap.height);
        const canvas = document.createElement('canvas');
        canvas.width = Math.round(bitmap.width * scale);
        canvas.height = Math.round(bitmap.height * scale);
        canvas.getContext('2d').drawImage(bitmap, 0, 0, canvas.width, canvas.height);
        const blob = await new Promise(resolve => canvas.toBlob(resolve, 'image/png'));
        return new File([blob], 'scan.png', { type: 'image/png' });
    };

    const decodePhoto = async file => {
        const reader = new Html5Qrcode('fileReader', options);
        const attempt = async image => {
            try { return await reader.scanFile(image, false); } catch { return null; }
        };
        let text = await attempt(file);
        if (text) return text;
        const bitmap = await createImageBitmap(file);
        for (const side of [1000, 700, 500, 360, 260, 180, 130]) {
            text = await attempt(await resized(bitmap, side));
            if (text) return text;
        }
        return null;
    };

    fileInput.addEventListener('change', async () => {
        const file = fileInput.files[0];
        if (!file) return;
        show('');
        done = false;
        await stop();
        let text = null;
        try {
            text = await decodePhoto(file);
        } catch {
            // 非圖片或無法解碼的檔案，下方顯示提示
        } finally {
            fileInput.value = '';
        }
        if (text) handle(text);
        else show('照片中找不到 QR Code，請靠近一點重拍，或手動輸入資產編號。');
    });
})();
