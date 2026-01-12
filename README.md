# Setto SDK for Unity

Setto Unity SDK - 시스템 브라우저 및 WebGL 기반 결제 연동 SDK

## 요구사항

- Unity 2019.4+

## 지원 플랫폼

| 플랫폼 | 방식 | 결과 수신 |
|--------|------|-----------|
| iOS | 시스템 브라우저 | Custom URL Scheme |
| Android | 시스템 브라우저 | Custom URL Scheme |
| Windows/Mac | 시스템 브라우저 | Custom URL Scheme |
| WebGL | iframe + postMessage | JavaScript 콜백 |

## 설치

### Unity Package Manager (Git URL)

1. Window → Package Manager
2. + 버튼 → Add package from git URL...
3. URL 입력: `https://github.com/settopay-app/setto-unity-sdk.git`

### Manual Installation

1. 이 폴더를 `Assets/Plugins/SettoSDK`에 복사

## 설정

### 모바일/PC - Custom URL Scheme

앱/PC 빌드에서는 Deep Link로 결과를 수신합니다.

#### iOS

Build Settings → Player Settings → Other Settings → Supported URL Schemes에 추가:
```
mygame
```

또는 Xcode에서 Info.plist 직접 수정:
```xml
<key>CFBundleURLTypes</key>
<array>
    <dict>
        <key>CFBundleURLSchemes</key>
        <array>
            <string>mygame</string>
        </array>
    </dict>
</array>
```

#### Android

`Assets/Plugins/Android/AndroidManifest.xml`:
```xml
<activity android:name="com.unity3d.player.UnityPlayerActivity"
    android:launchMode="singleTask">
    <intent-filter>
        <action android:name="android.intent.action.VIEW" />
        <category android:name="android.intent.category.DEFAULT" />
        <category android:name="android.intent.category.BROWSABLE" />
        <data android:scheme="mygame" />
    </intent-filter>
</activity>
```

#### Windows/Mac

Installer에서 레지스트리(Windows) 또는 Info.plist(Mac) 설정 필요.

### Deep Link 수신 (네이티브 플러그인)

Unity에서 Deep Link를 수신하려면 플랫폼별 네이티브 코드가 필요합니다.
자세한 내용은 [Multi-Platform 기술 가이드](../../docs/multi_platform_details.md)를 참조하세요.

간단한 방법: [Unity Deep Linking 에셋](https://assetstore.unity.com/)을 사용하여 `SettoSDK.Instance.HandleDeepLink(url)` 호출

## 사용법

### SDK 초기화

```csharp
using Setto.SDK;

public class GameManager : MonoBehaviour
{
    void Start()
    {
        SettoSDK.Instance.Initialize(
            merchantId: "your-merchant-id",
            environment: SettoEnvironment.Production,
            returnScheme: "mygame"  // WebGL에서는 무시됨
        );
    }
}
```

### 결제 요청

```csharp
using Setto.SDK;

public void HandlePayment()
{
    var paymentParams = new PaymentParams
    {
        OrderId = "order-123",
        Amount = 100.00m,
        Currency = "USD",       // 선택
        IdpToken = idpToken     // WebGL 전용 (선택)
    };

    SettoSDK.Instance.OpenPayment(paymentParams, result =>
    {
        switch (result.Status)
        {
            case PaymentStatus.Success:
                Debug.Log($"결제 성공! TX ID: {result.txId}");
                // 서버에서 결제 검증 필수!
                break;

            case PaymentStatus.Cancelled:
                Debug.Log("사용자가 결제를 취소했습니다.");
                break;

            case PaymentStatus.Failed:
                Debug.LogError($"결제 실패: {result.error}");
                break;
        }
    });
}
```

### Deep Link 처리 (앱/PC)

앱/PC에서 Deep Link로 결과를 수신하면 `HandleDeepLink`를 호출합니다:

```csharp
// 플랫폼별 Deep Link 수신 후 호출
public void OnDeepLinkReceived(string url)
{
    SettoSDK.Instance.HandleDeepLink(url);
}
```

## API

### SettoSDK

#### `Initialize(merchantId, environment, returnScheme)`

SDK를 초기화합니다.

| 파라미터 | 타입 | 설명 |
|---------|------|------|
| `merchantId` | `string` | 고객사 ID |
| `environment` | `SettoEnvironment` | `Development` 또는 `Production` |
| `returnScheme` | `string` | Custom URL Scheme (WebGL에서는 무시) |

#### `OpenPayment(params, onComplete)`

결제 창을 열고 결제를 진행합니다.

| 파라미터 | 타입 | 설명 |
|---------|------|------|
| `params` | `PaymentParams` | 결제 파라미터 |
| `onComplete` | `Action<PaymentResult>` | 결제 완료 콜백 |

#### `HandleDeepLink(url)`

Deep Link를 처리합니다. 앱/PC에서 Deep Link 수신 시 호출합니다.

### PaymentParams

| 속성 | 타입 | 필수 | 설명 |
|------|------|------|------|
| `OrderId` | `string` | ✅ | 주문 ID |
| `Amount` | `decimal` | ✅ | 결제 금액 |
| `Currency` | `string` | | 통화 (기본: USD) |
| `IdpToken` | `string` | | WebGL 전용: IdP Token |

### PaymentResult

| 속성 | 타입 | 설명 |
|------|------|------|
| `Status` | `PaymentStatus` | `Success`, `Failed`, `Cancelled` |
| `txId` | `string` | 블록체인 트랜잭션 해시 |
| `paymentId` | `string` | Setto 결제 ID |
| `error` | `string` | 에러 메시지 |

### SettoErrorCode

| 값 | 설명 |
|----|------|
| `UserCancelled` | 사용자 취소 |
| `PaymentFailed` | 결제 실패 |
| `InsufficientBalance` | 잔액 부족 |
| `TransactionRejected` | 트랜잭션 거부 |
| `NetworkError` | 네트워크 오류 |
| `SessionExpired` | 세션 만료 |
| `InvalidParams` | 잘못된 파라미터 |
| `InvalidMerchant` | 유효하지 않은 고객사 |

## 보안 참고사항

1. **결제 결과는 서버에서 검증 필수**: SDK에서 반환하는 결과는 UX 피드백용입니다. 실제 결제 완료 여부는 고객사 서버에서 Setto API를 통해 검증해야 합니다.

2. **Custom URL Scheme 보안**: 다른 앱이 동일한 Scheme을 등록할 수 있으므로, 결제 결과는 반드시 서버에서 검증하세요.

## License

MIT
