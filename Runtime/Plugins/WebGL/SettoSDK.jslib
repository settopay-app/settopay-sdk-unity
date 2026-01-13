mergeInto(LibraryManager.library, {
  SettoSDK_OpenPayment: function(urlPtr, callbackObjectNamePtr) {
    var url = UTF8ToString(urlPtr);
    var callbackObjectName = UTF8ToString(callbackObjectNamePtr);

    // overlay 생성 (전체 화면, 투명)
    var overlay = document.createElement('div');
    overlay.id = 'setto-overlay';
    overlay.style.cssText = 'position:fixed;top:0;left:0;width:100%;height:100%;background:transparent;z-index:99999;pointer-events:none;';

    // iframe 생성 (전체 화면 - 내부에서 바닥 모달 스타일 처리)
    var iframe = document.createElement('iframe');
    iframe.id = 'setto-iframe';
    iframe.src = url;
    iframe.allow = 'clipboard-write';
    iframe.style.cssText = 'position:absolute;top:0;left:0;width:100%;height:100%;border:none;background:transparent;pointer-events:auto;';

    overlay.appendChild(iframe);
    document.body.appendChild(overlay);

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
          txHash: data.txHash || '',
          fromAddress: data.fromAddress || '',
          toAddress: data.toAddress || '',
          amount: data.amount || '',
          chainId: data.chainId || 0,
          tokenSymbol: data.tokenSymbol || ''
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
