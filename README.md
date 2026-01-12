# Setto SDK for Unity

Unity 게임에서 Setto 결제를 연동하기 위한 SDK입니다.

## 지원 플랫폼

| 플랫폼 | 지원 | 결제 방식 |
|--------|------|----------|
| WebGL | ✅ | iframe + postMessage |
| iOS | ✅ | SFSafariViewController + URL Scheme |
| Android | ✅ | Chrome Custom Tabs + URL Scheme |
| Desktop (Editor) | ⚠️ | 시스템 브라우저 (콜백 미지원) |

## 설치

### Unity Package Manager (권장)

1. Unity Editor에서 `Window > Package Manager` 열기
2. `+` 버튼 클릭 → `Add package from git URL...`
3. 다음 URL 입력:
   ```
   https://github.com/anthropics/setto-sdk-unity.git
   ```

### 수동 설치

1. 이 저장소를 다운로드
2. `Runtime` 폴더를 Unity 프로젝트의 `Assets/Plugins/Setto` 폴더에 복사

## 사용법

### 1. SDK 초기화

```csharp
using Setto.SDK;

void Start()
{
    var config = new SettoConfig
    {
        environment = SettoEnvironment.Dev, // 또는 Prod
        // idpToken = "firebase-id-token", // 선택: 있으면 자동로그인
        debug = true
    };

    SettoSDK.Instance.Initialize(config);
}
```

### 2. 결제 요청

```csharp
public void OnPayButtonClicked()
{
    SettoSDK.Instance.OpenPayment(
        merchantId: "YOUR_MERCHANT_ID",  // 상점 ID (멀티 머천트 지원)
        amount: "10",           // USD 금액
        callback: OnPaymentResult
    );
}

private void OnPaymentResult(PaymentResult result)
{
    switch (result.status)
    {
        case PaymentStatus.Success:
            Debug.Log($"Payment Success! TxHash: {result.txHash}");
            break;
        case PaymentStatus.Failed:
            Debug.LogError($"Payment Failed: {result.error}");
            break;
        case PaymentStatus.Cancelled:
            Debug.Log("Payment Cancelled");
            break;
    }
}
```

## 자동로그인 (IdP Token)

고객사 게임에서 이미 Firebase 등으로 사용자 인증이 되어 있다면, IdP Token을 전달하여 Setto 로그인을 스킵할 수 있습니다.

```csharp
var config = new SettoConfig
{
    environment = SettoEnvironment.Dev,
    idpToken = firebaseIdToken, // Firebase ID Token
    debug = true
};

SettoSDK.Instance.Initialize(config);
```

- IdP Token 없음: Setto 로그인 화면 표시
- IdP Token 있음: 로그인 스킵, 바로 지갑 선택 화면

## 플랫폼별 설정

### iOS

`Info.plist`에 URL Scheme 등록:

```xml
<key>CFBundleURLTypes</key>
<array>
    <dict>
        <key>CFBundleURLSchemes</key>
        <array>
            <string>setto-YOUR_MERCHANT_ID</string>
        </array>
    </dict>
</array>
```

`AppDelegate.mm`에서 딥링크 처리:

```objc
- (BOOL)application:(UIApplication *)app openURL:(NSURL *)url options:(NSDictionary *)options {
    if ([url.scheme hasPrefix:@"setto-"]) {
        SettoSDK_iOS_HandleURL([url.absoluteString UTF8String]);
        return YES;
    }
    return NO;
}
```

### Android

`AndroidManifest.xml`에 URL Scheme 등록:

```xml
<activity android:name="com.unity3d.player.UnityPlayerActivity" ...>
    <intent-filter>
        <action android:name="android.intent.action.VIEW" />
        <category android:name="android.intent.category.DEFAULT" />
        <category android:name="android.intent.category.BROWSABLE" />
        <data android:scheme="setto-YOUR_MERCHANT_ID" android:host="callback" />
    </intent-filter>
</activity>
```

`build.gradle`에 Chrome Custom Tabs 의존성 추가:

```gradle
dependencies {
    implementation 'androidx.browser:browser:1.5.0'
}
```

딥링크 처리 (Unity 2019.4+에서는 자동 처리됨):

```csharp
// Application.deepLinkActivated 이벤트 사용
void OnEnable()
{
    Application.deepLinkActivated += OnDeepLinkActivated;
}

void OnDeepLinkActivated(string url)
{
    SettoSDK.Instance.HandleDeepLink(url);
}
```

### WebGL

추가 설정 없이 바로 동작합니다.

## 샘플

Package Manager에서 `Samples` 섹션의 `Basic Example`을 Import하면 바로 테스트 가능한 예제를 확인할 수 있습니다.

## 요구사항

- Unity 2021.3 이상
- iOS: iOS 12.0 이상
- Android: API Level 21 이상

## 주의사항

- Editor에서는 시스템 브라우저가 열리며, 결제 결과 콜백을 받을 수 없습니다
- 프로덕션 배포 전 `environment`를 `Prod`로 변경하세요
- iOS/Android에서는 반드시 URL Scheme을 등록해야 콜백을 받을 수 있습니다

## 지원

- 문서: https://docs.settopay.com/sdk/unity
- 이슈: https://github.com/anthropics/setto-sdk-unity/issues
