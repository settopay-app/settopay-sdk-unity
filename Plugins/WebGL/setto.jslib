mergeInto(LibraryManager.library, {
    // Setto WebGL SDK
    // wallet.settopay.com과 iframe + postMessage로 통신합니다.
    //
    // MESSAGE_TYPES (wallet.settopay.com과 동기화 필요)
    // - INIT_PAYMENT: 'INIT_PAYMENT'
    // - PAYMENT_SUCCESS: 'PAYMENT_SUCCESS'
    // - PAYMENT_FAILED: 'PAYMENT_FAILED'
    // - PAYMENT_CANCELLED: 'PAYMENT_CANCELLED'

    SettoOpenPayment: function(merchantIdPtr, orderIdPtr, amountPtr, currencyPtr, idpTokenPtr, baseUrlPtr) {
        var merchantId = UTF8ToString(merchantIdPtr);
        var orderId = UTF8ToString(orderIdPtr);
        var amount = UTF8ToString(amountPtr);
        var currency = UTF8ToString(currencyPtr);
        var idpToken = UTF8ToString(idpTokenPtr);
        var baseUrl = UTF8ToString(baseUrlPtr);

        // 오버레이 생성
        var overlay = document.createElement('div');
        overlay.id = 'setto-overlay';
        overlay.style.cssText = 'position:fixed;top:0;left:0;width:100%;height:100%;background:rgba(0,0,0,0.5);z-index:9999;';

        // iframe 생성
        var iframe = document.createElement('iframe');
        iframe.id = 'setto-iframe';
        iframe.src = baseUrl + '/embed';
        iframe.style.cssText = 'position:fixed;top:50%;left:50%;transform:translate(-50%,-50%);width:400px;height:600px;max-width:95vw;max-height:90vh;border:none;border-radius:12px;z-index:10000;';

        document.body.appendChild(overlay);
        document.body.appendChild(iframe);

        // message handler 참조 저장
        var messageHandler = null;

        // cleanup 함수
        var cleanup = function() {
            var iframeEl = document.getElementById('setto-iframe');
            var overlayEl = document.getElementById('setto-overlay');
            if (iframeEl) iframeEl.remove();
            if (overlayEl) overlayEl.remove();
            if (messageHandler) {
                window.removeEventListener('message', messageHandler);
            }
        };

        // overlay 클릭 시 취소 처리
        overlay.onclick = function(e) {
            if (e.target === overlay) {
                cleanup();
                SendMessage('SettoSDK', 'OnPaymentResult',
                    JSON.stringify({ status: 'cancelled', txId: null, paymentId: null, error: null }));
            }
        };

        // iframe 로드 완료 시 결제 초기화
        iframe.onload = function() {
            var message = {
                type: 'INIT_PAYMENT',
                merchantId: merchantId,
                orderId: orderId,
                amount: parseFloat(amount)
            };

            if (currency) message.currency = currency;
            if (idpToken) message.idpToken = idpToken;

            iframe.contentWindow.postMessage(message, baseUrl);
        };

        // 결제 결과 메시지 수신
        messageHandler = function(event) {
            if (event.origin !== baseUrl) return;

            var type = event.data.type;
            var data = event.data.data || {};

            if (type === 'PAYMENT_SUCCESS' || type === 'PAYMENT_FAILED' || type === 'PAYMENT_CANCELLED') {
                cleanup();

                // Unity로 결과 전달
                var result = {
                    status: type === 'PAYMENT_SUCCESS' ? 'success' :
                            type === 'PAYMENT_CANCELLED' ? 'cancelled' : 'failed',
                    txId: data.txId || null,
                    paymentId: data.paymentId || null,
                    error: data.error || null
                };

                SendMessage('SettoSDK', 'OnPaymentResult', JSON.stringify(result));
            }
        };

        window.addEventListener('message', messageHandler);
    },

    SettoClosePayment: function() {
        var iframe = document.getElementById('setto-iframe');
        var overlay = document.getElementById('setto-overlay');
        if (iframe) iframe.remove();
        if (overlay) overlay.remove();
    }
});
