#import <Foundation/Foundation.h>
#import <SafariServices/SafariServices.h>
#import <UIKit/UIKit.h>

// Unity에서 호출할 콜백 오브젝트 이름 저장
static NSString *_callbackObjectName = nil;
static SFSafariViewController *_safariVC = nil;

// Unity로 메시지 전송
extern "C" void UnitySendMessage(const char* obj, const char* method, const char* msg);

// URL Scheme 딥링크 처리를 위한 Observer
@interface SettoSDKURLHandler : NSObject
+ (instancetype)sharedInstance;
- (void)handleURL:(NSURL *)url;
@end

@implementation SettoSDKURLHandler

+ (instancetype)sharedInstance {
    static SettoSDKURLHandler *instance = nil;
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        instance = [[SettoSDKURLHandler alloc] init];
    });
    return instance;
}

- (void)handleURL:(NSURL *)url {
    if (_callbackObjectName == nil) return;

    NSURLComponents *components = [NSURLComponents componentsWithURL:url resolvingAgainstBaseURL:NO];
    NSString *status = nil;
    NSString *paymentId = nil;
    NSString *txHash = nil;
    NSString *error = nil;

    for (NSURLQueryItem *item in components.queryItems) {
        if ([item.name isEqualToString:@"status"]) {
            status = item.value;
        } else if ([item.name isEqualToString:@"payment_id"]) {
            paymentId = item.value;
        } else if ([item.name isEqualToString:@"tx_hash"]) {
            txHash = item.value;
        } else if ([item.name isEqualToString:@"error"]) {
            error = item.value;
        }
    }

    // JSON 결과 생성
    NSMutableDictionary *result = [NSMutableDictionary dictionary];

    if ([status isEqualToString:@"success"]) {
        result[@"status"] = @0; // PaymentStatus.Success
        if (paymentId) result[@"paymentId"] = paymentId;
        if (txHash) result[@"txHash"] = txHash;
    } else if ([status isEqualToString:@"failed"]) {
        result[@"status"] = @1; // PaymentStatus.Failed
        if (error) result[@"error"] = error;
    } else {
        result[@"status"] = @2; // PaymentStatus.Cancelled
    }

    NSData *jsonData = [NSJSONSerialization dataWithJSONObject:result options:0 error:nil];
    NSString *jsonString = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];

    // Safari 닫기
    if (_safariVC) {
        [_safariVC dismissViewControllerAnimated:YES completion:nil];
        _safariVC = nil;
    }

    // Unity로 결과 전송
    UnitySendMessage([_callbackObjectName UTF8String], "OnPaymentResult", [jsonString UTF8String]);
    _callbackObjectName = nil;
}

@end

// MARK: - C Interface (Unity에서 호출)

extern "C" {

    void SettoSDK_iOS_OpenPayment(const char* urlStr, const char* callbackObjectName) {
        _callbackObjectName = [NSString stringWithUTF8String:callbackObjectName];

        NSURL *url = [NSURL URLWithString:[NSString stringWithUTF8String:urlStr]];

        dispatch_async(dispatch_get_main_queue(), ^{
            UIViewController *rootVC = [UIApplication sharedApplication].keyWindow.rootViewController;

            // 최상위 ViewController 찾기
            while (rootVC.presentedViewController) {
                rootVC = rootVC.presentedViewController;
            }

            _safariVC = [[SFSafariViewController alloc] initWithURL:url];
            _safariVC.modalPresentationStyle = UIModalPresentationPageSheet;

            // iOS 15+ 에서 시트 크기 조절
            if (@available(iOS 15.0, *)) {
                _safariVC.sheetPresentationController.detents = @[
                    UISheetPresentationControllerDetent.largeDetent
                ];
                _safariVC.sheetPresentationController.prefersGrabberVisible = YES;
            }

            [rootVC presentViewController:_safariVC animated:YES completion:nil];
        });
    }

    void SettoSDK_iOS_ClosePayment() {
        dispatch_async(dispatch_get_main_queue(), ^{
            if (_safariVC) {
                [_safariVC dismissViewControllerAnimated:YES completion:nil];
                _safariVC = nil;
            }

            if (_callbackObjectName) {
                NSDictionary *result = @{@"status": @2}; // Cancelled
                NSData *jsonData = [NSJSONSerialization dataWithJSONObject:result options:0 error:nil];
                NSString *jsonString = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];

                UnitySendMessage([_callbackObjectName UTF8String], "OnPaymentResult", [jsonString UTF8String]);
                _callbackObjectName = nil;
            }
        });
    }

    // AppDelegate에서 호출해야 함
    void SettoSDK_iOS_HandleURL(const char* urlStr) {
        NSURL *url = [NSURL URLWithString:[NSString stringWithUTF8String:urlStr]];
        [[SettoSDKURLHandler sharedInstance] handleURL:url];
    }
}
