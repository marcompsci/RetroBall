// Retro Hoops — Live online matches (original code). Called from C# via [DllImport("__Internal")].
// Uses Apple's Game Center real-time matchmaking (GKMatchmaker / GKMatch): Apple finds an opponent
// and relays the messages, so the game needs no server of its own. Only players in the same
// playerGroup (same game version) are matched. Messages are small byte arrays, reliable and in order.
#import <Foundation/Foundation.h>
#import <GameKit/GameKit.h>
#include <string.h>
#include <stdlib.h>

// 0 idle, 1 searching, 2 connected, 3 lost/failed.
enum { RLVIdle = 0, RLVSearching = 1, RLVConnected = 2, RLVLost = 3 };

@interface RetroLiveManager : NSObject <GKMatchDelegate>
@property (nonatomic, strong) GKMatch* match;
@property (nonatomic, strong) NSMutableArray<NSData*>* inbox;
@property (nonatomic, strong) NSString* opponentName;
@property (nonatomic, strong) NSString* opponentId;
@property (nonatomic, strong) NSString* error;
@property (nonatomic, assign) int state;
// Game Center identity proof for the Retro Hoops Live server (0 none, 1 fetching, 2 ready, 3 failed).
@property (nonatomic, assign) int idState;
@property (nonatomic, strong) NSString* idKeyUrl;
@property (nonatomic, strong) NSString* idSignature;
@property (nonatomic, strong) NSString* idSalt;
@property (nonatomic, assign) double idTimestamp;
@end

@implementation RetroLiveManager

- (instancetype)init
{
    if ((self = [super init]))
    {
        _inbox = [[NSMutableArray alloc] init];
        _opponentName = @"";
        _opponentId = @"";
        _error = @"";
        _state = RLVIdle;
    }
    return self;
}

- (void)findWithGroup:(NSUInteger)group
{
    [self teardown];
    @synchronized (self)
    {
        [self.inbox removeAllObjects];
        self.opponentName = @"";
        self.opponentId = @"";
        self.error = @"";
        self.state = RLVSearching;
    }
    if (![GKLocalPlayer localPlayer].isAuthenticated)
    {
        @synchronized (self) { self.state = RLVLost; self.error = @"Sign in to Game Center first (Settings ▸ GAME CENTER)."; }
        return;
    }
    GKMatchRequest* request = [[GKMatchRequest alloc] init];
    request.minPlayers = 2;
    request.maxPlayers = 2;
    request.playerGroup = group;
    [[GKMatchmaker sharedMatchmaker] findMatchForRequest:request withCompletionHandler:^(GKMatch* match, NSError* error) {
        dispatch_async(dispatch_get_main_queue(), ^{
            if (error != nil || match == nil)
            {
                @synchronized (self)
                {
                    if (self.state == RLVSearching)
                    {
                        self.state = RLVLost;
                        self.error = error != nil ? error.localizedDescription : @"No match found.";
                    }
                }
                return;
            }
            @synchronized (self)
            {
                if (self.state != RLVSearching) { [match disconnect]; return; } // cancelled meanwhile
            }
            self.match = match;
            match.delegate = self;
            [self checkConnected];
        });
    }];
}

- (void)checkConnected
{
    GKMatch* m = self.match;
    if (m == nil || m.expectedPlayerCount > 0 || m.players.count == 0) return;
    GKPlayer* other = m.players[0];
    @synchronized (self)
    {
        self.opponentName = other.displayName ?: @"";
        // teamPlayerID: the same id the server verifies at sign-in (both phones use it to pick the host).
        self.opponentId = other.teamPlayerID ?: @"";
        self.state = RLVConnected;
    }
}

- (void)teardown
{
    if (self.match != nil)
    {
        self.match.delegate = nil;
        [self.match disconnect];
        self.match = nil;
    }
}

- (void)stop
{
    [[GKMatchmaker sharedMatchmaker] cancel];
    [self teardown];
    @synchronized (self)
    {
        [self.inbox removeAllObjects];
        self.state = RLVIdle;
    }
}

- (BOOL)send:(NSData*)data
{
    GKMatch* m = self.match;
    if (m == nil) return NO;
    NSError* error = nil;
    BOOL ok = [m sendDataToAllPlayers:data withDataMode:GKMatchSendDataReliable error:&error];
    return ok && error == nil;
}

// ------------------------------------------------------------------ GKMatchDelegate

- (void)match:(GKMatch*)match didReceiveData:(NSData*)data fromRemotePlayer:(GKPlayer*)player
{
    @synchronized (self) { [self.inbox addObject:data]; }
}

- (void)match:(GKMatch*)match player:(GKPlayer*)player didChangeConnectionState:(GKPlayerConnectionState)state
{
    if (state == GKPlayerStateConnected)
    {
        dispatch_async(dispatch_get_main_queue(), ^{ [self checkConnected]; });
    }
    else if (state == GKPlayerStateDisconnected)
    {
        @synchronized (self) { if (self.state != RLVIdle) self.state = RLVLost; }
    }
}

- (void)match:(GKMatch*)match didFailWithError:(NSError*)error
{
    @synchronized (self)
    {
        self.state = RLVLost;
        self.error = error != nil ? error.localizedDescription : @"The connection failed.";
    }
}

@end

static RetroLiveManager* RetroLiveShared(void)
{
    static RetroLiveManager* shared = nil;
    static dispatch_once_t once;
    dispatch_once(&once, ^{ shared = [[RetroLiveManager alloc] init]; });
    return shared;
}

static char* RetroLiveCopy(NSString* s)
{
    const char* utf8 = [(s ?: @"") UTF8String];
    if (utf8 == NULL) utf8 = "";
    char* copy = (char*)malloc(strlen(utf8) + 1);
    strcpy(copy, utf8);
    return copy; // freed by the C# marshaller
}

extern "C" {

void RetroLive_Find(int group)
{
    RetroLiveManager* m = RetroLiveShared();
    @synchronized (m) { m.state = RLVSearching; }
    dispatch_async(dispatch_get_main_queue(), ^{ [m findWithGroup:(NSUInteger)(group > 0 ? group : 1)]; });
}

void RetroLive_Stop(void)
{
    RetroLiveManager* m = RetroLiveShared();
    @synchronized (m) { m.state = RLVIdle; [m.inbox removeAllObjects]; }
    dispatch_async(dispatch_get_main_queue(), ^{ [m stop]; });
}

int RetroLive_State(void)
{
    RetroLiveManager* m = RetroLiveShared();
    @synchronized (m) { return m.state; }
}

char* RetroLive_Error(void)
{
    RetroLiveManager* m = RetroLiveShared();
    @synchronized (m) { return RetroLiveCopy(m.error); }
}

char* RetroLive_OpponentName(void)
{
    RetroLiveManager* m = RetroLiveShared();
    @synchronized (m) { return RetroLiveCopy(m.opponentName); }
}

char* RetroLive_OpponentId(void)
{
    RetroLiveManager* m = RetroLiveShared();
    @synchronized (m) { return RetroLiveCopy(m.opponentId); }
}

char* RetroLive_LocalId(void)
{
    return RetroLiveCopy([GKLocalPlayer localPlayer].teamPlayerID);
}

void RetroLive_FetchIdentity(void)
{
    RetroLiveManager* m = RetroLiveShared();
    @synchronized (m) { m.idState = 1; }
    if (![GKLocalPlayer localPlayer].isAuthenticated)
    {
        @synchronized (m) { m.idState = 3; }
        return;
    }
    [[GKLocalPlayer localPlayer] fetchItemsForIdentityVerificationSignature:^(NSURL* publicKeyURL, NSData* signature, NSData* salt, uint64_t timestamp, NSError* error) {
        @synchronized (m)
        {
            if (error != nil || publicKeyURL == nil || signature == nil || salt == nil)
            {
                m.idState = 3;
                m.error = error != nil ? error.localizedDescription : @"Game Center didn't sign in.";
                return;
            }
            m.idKeyUrl = publicKeyURL.absoluteString ?: @"";
            m.idSignature = [signature base64EncodedStringWithOptions:0];
            m.idSalt = [salt base64EncodedStringWithOptions:0];
            m.idTimestamp = (double)timestamp;
            m.idState = 2;
        }
    }];
}

int RetroLive_IdentityState(void)
{
    RetroLiveManager* m = RetroLiveShared();
    @synchronized (m) { return m.idState; }
}

char* RetroLive_IdentityKeyUrl(void)
{
    RetroLiveManager* m = RetroLiveShared();
    @synchronized (m) { return RetroLiveCopy(m.idKeyUrl); }
}

char* RetroLive_IdentitySignature(void)
{
    RetroLiveManager* m = RetroLiveShared();
    @synchronized (m) { return RetroLiveCopy(m.idSignature); }
}

char* RetroLive_IdentitySalt(void)
{
    RetroLiveManager* m = RetroLiveShared();
    @synchronized (m) { return RetroLiveCopy(m.idSalt); }
}

double RetroLive_IdentityTimestamp(void)
{
    RetroLiveManager* m = RetroLiveShared();
    @synchronized (m) { return m.idTimestamp; }
}

char* RetroLive_LocalName(void)
{
    return RetroLiveCopy([GKLocalPlayer localPlayer].displayName);
}

int RetroLive_Send(const unsigned char* data, int length)
{
    if (data == NULL || length <= 0) return 0;
    NSData* d = [NSData dataWithBytes:data length:(NSUInteger)length];
    return [RetroLiveShared() send:d] ? 1 : 0;
}

int RetroLive_NextLength(void)
{
    RetroLiveManager* m = RetroLiveShared();
    @synchronized (m)
    {
        if (m.inbox.count == 0) return -1;
        return (int)m.inbox[0].length;
    }
}

int RetroLive_Receive(unsigned char* buffer, int capacity)
{
    RetroLiveManager* m = RetroLiveShared();
    @synchronized (m)
    {
        if (m.inbox.count == 0 || buffer == NULL) return -1;
        NSData* d = m.inbox[0];
        if ((int)d.length > capacity) return -1;
        memcpy(buffer, d.bytes, d.length);
        [m.inbox removeObjectAtIndex:0];
        return (int)d.length;
    }
}

}
