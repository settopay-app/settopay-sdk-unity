package com.setto.sdk;

import android.app.Activity;
import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;

import androidx.browser.customtabs.CustomTabsIntent;

import com.unity3d.player.UnityPlayer;

/**
 * Setto SDK Android Plugin
 *
 * Chrome Custom Tabs를 사용하여 결제 페이지를 열고,
 * URL Scheme 딥링크로 결과를 수신합니다.
 */
public class SettoSDK {

    private static String callbackObjectName = null;

    /**
     * Chrome Custom Tabs로 결제 페이지 열기
     *
     * @param url 결제 페이지 URL
     * @param objectName Unity 콜백 오브젝트 이름
     */
    public static void openPayment(String url, String objectName) {
        callbackObjectName = objectName;

        Activity activity = UnityPlayer.currentActivity;
        if (activity == null) {
            sendResult("{\"status\":1,\"error\":\"Activity not found\"}");
            return;
        }

        activity.runOnUiThread(() -> {
            try {
                CustomTabsIntent.Builder builder = new CustomTabsIntent.Builder();

                // Bottom Sheet 스타일 (Android 12+)
                // builder.setInitialActivityHeightPx(1200);

                CustomTabsIntent customTabsIntent = builder.build();
                customTabsIntent.launchUrl(activity, Uri.parse(url));

            } catch (Exception e) {
                // Chrome Custom Tabs 미지원 시 기본 브라우저 사용
                try {
                    Intent intent = new Intent(Intent.ACTION_VIEW, Uri.parse(url));
                    activity.startActivity(intent);
                } catch (Exception e2) {
                    sendResult("{\"status\":1,\"error\":\"" + e2.getMessage() + "\"}");
                }
            }
        });
    }

    /**
     * 결제 취소 (수동 호출 시)
     */
    public static void closePayment() {
        if (callbackObjectName != null) {
            sendResult("{\"status\":2}"); // Cancelled
            callbackObjectName = null;
        }
    }

    /**
     * URL Scheme 딥링크 처리
     * Activity의 onNewIntent에서 호출해야 함
     *
     * @param url 딥링크 URL (setto-{merchantId}://callback?status=success&...)
     */
    public static void handleURL(String url) {
        if (callbackObjectName == null || url == null) return;

        try {
            Uri uri = Uri.parse(url);
            String status = uri.getQueryParameter("status");
            String paymentId = uri.getQueryParameter("payment_id");
            String txHash = uri.getQueryParameter("tx_hash");
            String error = uri.getQueryParameter("error");

            StringBuilder json = new StringBuilder("{");

            if ("success".equals(status)) {
                json.append("\"status\":0"); // PaymentStatus.Success
                if (paymentId != null) {
                    json.append(",\"paymentId\":\"").append(paymentId).append("\"");
                }
                if (txHash != null) {
                    json.append(",\"txHash\":\"").append(txHash).append("\"");
                }
            } else if ("failed".equals(status)) {
                json.append("\"status\":1"); // PaymentStatus.Failed
                if (error != null) {
                    json.append(",\"error\":\"").append(error).append("\"");
                }
            } else {
                json.append("\"status\":2"); // PaymentStatus.Cancelled
            }

            json.append("}");

            sendResult(json.toString());
            callbackObjectName = null;

        } catch (Exception e) {
            sendResult("{\"status\":1,\"error\":\"" + e.getMessage() + "\"}");
            callbackObjectName = null;
        }
    }

    /**
     * Unity로 결과 전송
     */
    private static void sendResult(String json) {
        if (callbackObjectName != null) {
            UnityPlayer.UnitySendMessage(callbackObjectName, "OnPaymentResult", json);
        }
    }
}
