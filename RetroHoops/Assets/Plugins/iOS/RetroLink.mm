// Retro Hoops — two-phone link (original code). Called from C# via [DllImport("__Internal")].
// Uses Apple's Multipeer Connectivity: nearby iPhones / iPads / Macs find each other over Wi-Fi
// or Bluetooth with no server and no internet. One phone hosts (advertises), the other browses
// and joins. Messages are small byte arrays, sent reliably and in order.
// Phase 30: up to three more phones can join a hosted game as watchers. The host talks to its one
// opponent (the "player") and separately sends the game to the watchers; data from anyone else is ignored.
// Info.plist needs NSLocalNetworkUsageDescription and NSBonjourServices (_retrohoops._tcp,
// _retrohoops._udp); IosPostProcess adds them and links MultipeerConnectivity.framework.
#import <Foundation/Foundation.h>
#import <MultipeerConnectivity/MultipeerConnectivity.h>
#include <string.h>
#include <stdlib.h>

static NSString* const kRetroLinkService = @"retrohoops";

// 0 idle, 1 searching (hosting or browsing), 2 connected, 3 lost.
enum { RLIdle = 0, RLSearching = 1, RLConnected = 2, RLLost = 3 };

@interface RetroLinkManager : NSObject <MCSessionDelegate, MCNearbyServiceAdvertiserDelegate, MCNearbyServiceBrowserDelegate>
@property (nonatomic, strong) MCPeerID* me;
// atomic: Multipeer's delegate callbacks (its own queue) read these while teardown (main thread) clears them.
@property (atomic, strong) MCSession* session;
@property (nonatomic, strong) MCNearbyServiceAdvertiser* advertiser;
@property (nonatomic, strong) MCNearbyServiceBrowser* browser;
@property (nonatomic, strong) NSMutableArray<MCPeerID*>* found;
@property (nonatomic, strong) NSMutableArray<NSData*>* inbox;
@property (nonatomic, strong) NSString* peerName;
@property (nonatomic, assign) int state;
@property (atomic, assign) BOOL hosting;
@property (nonatomic, assign) BOOL everConnected;
// Host: the opponent, and the phones watching. Guest / watcher: the host it joined.
@property (nonatomic, strong) MCPeerID* playerPeer;
@property (nonatomic, strong) MCPeerID* pendingPlayer;
@property (nonatomic, strong) MCPeerID* hostPeer;
@property (nonatomic, strong) NSMutableArray<MCPeerID*>* watchers;
@property (nonatomic, strong) NSMutableArray<MCPeerID*>* pendingWatchers;
@end

static const int kRetroLinkMaxWatchers = 3;

@implementation RetroLinkManager

- (instancetype)init
{
    if ((self = [super init]))
    {
        // alloc/init (owned) so this is right with or without ARC.
        _found = [[NSMutableArray alloc] init];
        _inbox = [[NSMutableArray alloc] init];
        _watchers = [[NSMutableArray alloc] init];
        _pendingWatchers = [[NSMutableArray alloc] init];
        _peerName = @"";
        _state = RLIdle;
    }
    return self;
}

- (void)startHosting:(BOOL)host name:(NSString*)name
{
    [self teardown];
    if (name.length == 0) name = @"Retro Hoops";
    // MCPeerID display names are limited to 63 bytes of UTF-8.
    // Trim whole characters (an emoji is two UTF-16 units; cutting one in half makes an invalid name).
    while (name.length > 0 && [name lengthOfBytesUsingEncoding:NSUTF8StringEncoding] > 63)
        name = [name substringToIndex:[name rangeOfComposedCharacterSequenceAtIndex:name.length - 1].location];
    if (name.length == 0) name = @"Retro Hoops";
    self.me = [[MCPeerID alloc] initWithDisplayName:name];
    self.session = [[MCSession alloc] initWithPeer:self.me securityIdentity:nil encryptionPreference:MCEncryptionRequired];
    self.session.delegate = self;
    self.hosting = host;
    self.everConnected = NO;
    @synchronized (self)
    {
        [self.found removeAllObjects];
        [self.inbox removeAllObjects];
        [self.watchers removeAllObjects];
        [self.pendingWatchers removeAllObjects];
        self.playerPeer = nil;
        self.pendingPlayer = nil;
        self.hostPeer = nil;
        self.peerName = @"";
        self.state = RLSearching;
    }
    if (host)
    {
        self.advertiser = [[MCNearbyServiceAdvertiser alloc] initWithPeer:self.me discoveryInfo:nil serviceType:kRetroLinkService];
        self.advertiser.delegate = self;
        [self.advertiser startAdvertisingPeer];
    }
    else
    {
        self.browser = [[MCNearbyServiceBrowser alloc] initWithPeer:self.me serviceType:kRetroLinkService];
        self.browser.delegate = self;
        [self.browser startBrowsingForPeers];
    }
}

- (void)stopLooking
{
    if (self.advertiser != nil)
    {
        [self.advertiser stopAdvertisingPeer];
        self.advertiser.delegate = nil;
        self.advertiser = nil;
    }
    if (self.browser != nil)
    {
        [self.browser stopBrowsingForPeers];
        self.browser.delegate = nil;
        self.browser = nil;
    }
}

- (void)teardown
{
    [self stopLooking];
    if (self.session != nil)
    {
        self.session.delegate = nil;
        [self.session disconnect];
        self.session = nil;
    }
}

- (void)stop
{
    [self teardown];
    @synchronized (self)
    {
        [self.found removeAllObjects];
        [self.inbox removeAllObjects];
        [self.watchers removeAllObjects];
        [self.pendingWatchers removeAllObjects];
        self.playerPeer = nil;
        self.pendingPlayer = nil;
        self.hostPeer = nil;
        self.state = RLIdle;
    }
}

- (BOOL)join:(int)index watch:(BOOL)watch
{
    MCPeerID* peer = nil;
    @synchronized (self)
    {
        if (index < 0 || index >= (int)self.found.count) return NO;
        peer = self.found[index];
        self.hostPeer = peer;
    }
    if (self.browser == nil || self.session == nil) return NO;
    NSData* context = [(watch ? @"watch" : @"play") dataUsingEncoding:NSUTF8StringEncoding];
    [self.browser invitePeer:peer toSession:self.session withContext:context timeout:20];
    return YES;
}

- (BOOL)sendTo:(NSArray<MCPeerID*>*)peers data:(NSData*)data
{
    MCSession* s = self.session;
    if (s == nil || peers.count == 0) return NO;
    NSError* error = nil;
    BOOL ok = [s sendData:data toPeers:peers withMode:MCSessionSendDataReliable error:&error];
    return ok && error == nil;
}

/// To the other player (host) or to the host (guest / watcher).
- (BOOL)send:(NSData*)data
{
    MCPeerID* to = nil;
    @synchronized (self) { to = self.hosting ? self.playerPeer : self.hostPeer; }
    if (to == nil || self.session == nil || ![self.session.connectedPeers containsObject:to]) return NO;
    return [self sendTo:@[ to ] data:data];
}

/// Host only: to every phone watching.
- (BOOL)sendWatchers:(NSData*)data
{
    NSArray<MCPeerID*>* to = nil;
    @synchronized (self) { to = [NSArray arrayWithArray:self.watchers]; }
    if (to.count == 0) return NO;
    return [self sendTo:to data:data];
}

- (int)watcherCount
{
    @synchronized (self) { return (int)self.watchers.count; }
}

// ------------------------------------------------------------------ MCNearbyServiceAdvertiserDelegate

- (void)advertiser:(MCNearbyServiceAdvertiser*)advertiser didReceiveInvitationFromPeer:(MCPeerID*)peerID
       withContext:(NSData*)context invitationHandler:(void (^)(BOOL accept, MCSession* session))invitationHandler
{
    // One opponent: the first "play" invitation. Watchers: once the game has its two players, up to three.
    NSString* kind = context != nil ? [[NSString alloc] initWithData:context encoding:NSUTF8StringEncoding] : nil;
    BOOL watch = kind != nil && [kind isEqualToString:@"watch"];
#if !__has_feature(objc_arc)
    [kind release];
#endif
    BOOL accept = NO;
    @synchronized (self)
    {
        if (self.session != nil)
        {
            if (watch)
            {
                accept = self.playerPeer != nil && (int)(self.watchers.count + self.pendingWatchers.count) < kRetroLinkMaxWatchers;
                if (accept) [self.pendingWatchers addObject:peerID];
            }
            else
            {
                accept = self.playerPeer == nil && self.pendingPlayer == nil;
                if (accept) self.pendingPlayer = peerID;
            }
        }
    }
    invitationHandler(accept, accept ? self.session : nil);
}

- (void)advertiser:(MCNearbyServiceAdvertiser*)advertiser didNotStartAdvertisingPeer:(NSError*)error
{
    NSLog(@"[Retro Hoops] Link: couldn't host: %@", error);
    @synchronized (self) { self.state = RLLost; }
}

// ------------------------------------------------------------------ MCNearbyServiceBrowserDelegate

- (void)browser:(MCNearbyServiceBrowser*)browser foundPeer:(MCPeerID*)peerID withDiscoveryInfo:(NSDictionary<NSString*, NSString*>*)info
{
    @synchronized (self)
    {
        for (MCPeerID* p in self.found) if ([p isEqual:peerID]) return;
        [self.found addObject:peerID];
    }
}

- (void)browser:(MCNearbyServiceBrowser*)browser lostPeer:(MCPeerID*)peerID
{
    @synchronized (self) { [self.found removeObject:peerID]; }
}

- (void)browser:(MCNearbyServiceBrowser*)browser didNotStartBrowsingForPeers:(NSError*)error
{
    NSLog(@"[Retro Hoops] Link: couldn't search: %@", error);
    @synchronized (self) { self.state = RLLost; }
}

// ------------------------------------------------------------------ MCSessionDelegate

- (void)session:(MCSession*)session peer:(MCPeerID*)peerID didChangeState:(MCSessionState)state
{
    if (state == MCSessionStateConnected)
    {
        BOOL connectedToGame = NO;
        @synchronized (self)
        {
            if (self.hosting)
            {
                if ([self.pendingWatchers containsObject:peerID])
                {
                    [self.pendingWatchers removeObject:peerID];
                    if (![self.watchers containsObject:peerID]) [self.watchers addObject:peerID];
                }
                else if (self.playerPeer == nil && (self.pendingPlayer == nil || [self.pendingPlayer isEqual:peerID]))
                {
                    self.playerPeer = peerID;
                    self.pendingPlayer = nil;
                    connectedToGame = YES;
                }
            }
            else if (self.hostPeer != nil && [self.hostPeer isEqual:peerID])
            {
                connectedToGame = YES;
            }
            if (connectedToGame)
            {
                self.peerName = peerID.displayName ?: @"";
                self.state = RLConnected;
                self.everConnected = YES;
            }
        }
        // The host keeps advertising so friends can find the game to watch it; a joining phone stops browsing.
        if (connectedToGame && !self.hosting) dispatch_async(dispatch_get_main_queue(), ^{ [self stopLooking]; });
    }
    else if (state == MCSessionStateNotConnected)
    {
        @synchronized (self)
        {
            [self.watchers removeObject:peerID];
            [self.pendingWatchers removeObject:peerID];
            if (self.pendingPlayer != nil && [self.pendingPlayer isEqual:peerID]) self.pendingPlayer = nil;
            BOOL theGame = self.hosting ? (self.playerPeer != nil && [self.playerPeer isEqual:peerID])
                                        : (self.hostPeer != nil && [self.hostPeer isEqual:peerID]);
            if (theGame || (!self.everConnected && !self.hosting))
            {
                // Lost after playing = the game is over; a failed join just goes back to searching.
                if (self.everConnected) self.state = RLLost;
                else if (self.state != RLIdle) self.state = RLSearching;
            }
        }
    }
}

- (void)session:(MCSession*)session didReceiveData:(NSData*)data fromPeer:(MCPeerID*)peerID
{
    @synchronized (self)
    {
        // Only the game's other end is listened to (watchers never decide anything).
        MCPeerID* from = self.hosting ? self.playerPeer : self.hostPeer;
        if (from != nil && [from isEqual:peerID]) [self.inbox addObject:data];
    }
}

- (void)session:(MCSession*)session didReceiveStream:(NSInputStream*)stream withName:(NSString*)streamName fromPeer:(MCPeerID*)peerID
{
}

- (void)session:(MCSession*)session didStartReceivingResourceWithName:(NSString*)resourceName fromPeer:(MCPeerID*)peerID withProgress:(NSProgress*)progress
{
}

- (void)session:(MCSession*)session didFinishReceivingResourceWithName:(NSString*)resourceName fromPeer:(MCPeerID*)peerID
          atURL:(NSURL*)localURL withError:(NSError*)error
{
}

@end

static RetroLinkManager* RetroLinkShared(void)
{
    static RetroLinkManager* shared = nil;
    static dispatch_once_t once;
    dispatch_once(&once, ^{ shared = [[RetroLinkManager alloc] init]; });
    return shared;
}

static char* RetroLinkCopy(NSString* s)
{
    const char* utf8 = [(s ?: @"") UTF8String];
    if (utf8 == NULL) utf8 = "";
    char* copy = (char*)malloc(strlen(utf8) + 1);
    strcpy(copy, utf8);
    return copy; // freed by the C# marshaller
}

extern "C" {

void RetroLink_Start(int host, const char* name)
{
    NSString* n = name != NULL ? [NSString stringWithUTF8String:name] : @"";
    RetroLinkManager* m = RetroLinkShared();
    dispatch_async(dispatch_get_main_queue(), ^{ [m startHosting:(host != 0) name:n]; });
    @synchronized (m) { m.state = RLSearching; }
}

void RetroLink_Stop(void)
{
    RetroLinkManager* m = RetroLinkShared();
    dispatch_async(dispatch_get_main_queue(), ^{ [m stop]; });
    @synchronized (m) { m.state = RLIdle; [m.inbox removeAllObjects]; }
}

int RetroLink_State(void)
{
    RetroLinkManager* m = RetroLinkShared();
    @synchronized (m) { return m.state; }
}

int RetroLink_FoundCount(void)
{
    RetroLinkManager* m = RetroLinkShared();
    @synchronized (m) { return (int)m.found.count; }
}

char* RetroLink_FoundName(int index)
{
    RetroLinkManager* m = RetroLinkShared();
    @synchronized (m)
    {
        if (index < 0 || index >= (int)m.found.count) return RetroLinkCopy(@"");
        return RetroLinkCopy(m.found[index].displayName);
    }
}

int RetroLink_Join(int index)
{
    RetroLinkManager* m = RetroLinkShared();
    dispatch_async(dispatch_get_main_queue(), ^{ [m join:index watch:NO]; });
    return 1;
}

int RetroLink_Watch(int index)
{
    RetroLinkManager* m = RetroLinkShared();
    dispatch_async(dispatch_get_main_queue(), ^{ [m join:index watch:YES]; });
    return 1;
}

int RetroLink_WatcherCount(void)
{
    return [RetroLinkShared() watcherCount];
}

int RetroLink_SendWatchers(const unsigned char* data, int length)
{
    if (data == NULL || length <= 0) return 0;
    NSData* d = [NSData dataWithBytes:data length:(NSUInteger)length];
    return [RetroLinkShared() sendWatchers:d] ? 1 : 0;
}

char* RetroLink_PeerName(void)
{
    RetroLinkManager* m = RetroLinkShared();
    @synchronized (m) { return RetroLinkCopy(m.peerName); }
}

int RetroLink_Send(const unsigned char* data, int length)
{
    if (data == NULL || length <= 0) return 0;
    NSData* d = [NSData dataWithBytes:data length:(NSUInteger)length];
    return [RetroLinkShared() send:d] ? 1 : 0;
}

int RetroLink_NextLength(void)
{
    RetroLinkManager* m = RetroLinkShared();
    @synchronized (m)
    {
        if (m.inbox.count == 0) return -1;
        return (int)m.inbox[0].length;
    }
}

int RetroLink_Receive(unsigned char* buffer, int capacity)
{
    RetroLinkManager* m = RetroLinkShared();
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
