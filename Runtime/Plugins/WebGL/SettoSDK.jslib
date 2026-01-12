mergeInto(LibraryManager.library, {
  SettoSDK_OpenPayment: function(urlPtr, callbackObjectNamePtr) {
    var url = UTF8ToString(urlPtr);
    var callbackObjectName = UTF8ToString(callbackObjectNamePtr);

    // iframe 생성
    var overlay = document.createElement('div');
    overlay.id = 'setto-overlay';
    overlay.style.cssText = 'position:fixed;top:0;left:0;width:100%;height:100%;background:rgba(0,0,0,0.5);z-index:99999;display:flex;align-items:center;justify-content:center;';

    var iframe = document.createElement('iframe');
    iframe.id = 'setto-iframe';
    iframe.src = url;
    iframe.style.cssText = 'width:420px;height:680px;max-width:95vw;max-height:90vh;border:none;border-radius:16px;background:white;';

    overlay.appendChild(iframe);
    document.body.appendChild(overlay);

    // 오버레이 클릭 시 취소
    overlay.addEventListener('click', function(e) {
      if (e.target === overlay) {
        cleanup();
        sendResult({ status: 2 }); // Cancelled
      }
    });

    // postMessage 수신
    function messageHandler(event) {
      var baseUrl = url.split('/pay/')[0];
      if (event.origin !== baseUrl) return;

      var type = event.data.type;
      var data = event.data.data || {};

      if (type === 'SETTO_PAYMENT_SUCCESS') {
        cleanup();
        sendResult({
          status: 0, // Success
          paymentId: data.paymentId || '',
          txHash: data.txHash || ''
        });
      } else if (type === 'SETTO_PAYMENT_FAILED') {
        cleanup();
        sendResult({
          status: 1, // Failed
          error: data.error || ''
        });
      } else if (type === 'SETTO_PAYMENT_CANCELLED') {
        cleanup();
        sendResult({ status: 2 }); // Cancelled
      }
    }

    window.addEventListener('message', messageHandler);

    function cleanup() {
      window.removeEventListener('message', messageHandler);
      if (overlay.parentNode) {
        overlay.parentNode.removeChild(overlay);
      }
    }

    function sendResult(result) {
      var json = JSON.stringify(result);
      SendMessage(callbackObjectName, 'OnPaymentResult', json);
    }

    // Unity가 접근할 수 있도록 저장
    window._settoCleanup = cleanup;
  },

  SettoSDK_ClosePayment: function() {
    if (window._settoCleanup) {
      window._settoCleanup();
      window._settoCleanup = null;
    }
  }
});
