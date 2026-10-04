// RetroBall — share sheet bridge (original code). Called from C# via [DllImport("__Internal")].
// Presents UIActivityViewController for a local file (e.g. a highlight GIF) plus optional text.
#import <UIKit/UIKit.h>

extern UIViewController* UnityGetGLViewController(void);

extern "C" {

void RetroShare_ShareFile(const char* path, const char* text)
{
    if (path == NULL) return;
    NSString* filePath = [NSString stringWithUTF8String:path];
    NSString* message = text != NULL ? [NSString stringWithUTF8String:text] : nil;
    dispatch_async(dispatch_get_main_queue(), ^{
        NSURL* url = [NSURL fileURLWithPath:filePath];
        NSMutableArray* items = [NSMutableArray arrayWithObject:url];
        if (message.length > 0) [items addObject:message];
        UIActivityViewController* vc = [[UIActivityViewController alloc] initWithActivityItems:items applicationActivities:nil];
        UIViewController* root = UnityGetGLViewController();
        if (root == nil) return;
        // iPad needs an anchor for the popover; harmless on iPhone.
        vc.popoverPresentationController.sourceView = root.view;
        vc.popoverPresentationController.sourceRect = CGRectMake(root.view.bounds.size.width * 0.5, root.view.bounds.size.height * 0.5, 1, 1);
        [root presentViewController:vc animated:YES completion:nil];
    });
}

}
