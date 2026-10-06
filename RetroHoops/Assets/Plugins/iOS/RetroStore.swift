// Retro Hoops — App Store subscription bridge (original code), StoreKit 2.
// Called from C# via [DllImport("__Internal")] through the @_cdecl functions at the bottom.
// One product: the Retro Hoops Live monthly auto-renewing subscription. The App Store decides
// whether it's active (Transaction.currentEntitlements); nothing is stored on any server of ours.
import Foundation
import StoreKit

// State codes read by C#: 0 loading, 1 not subscribed, 2 subscribed, 3 purchasing, 4 failed, 5 pending (Ask to Buy).
private let kLoading: Int32 = 0
private let kNotSubscribed: Int32 = 1
private let kSubscribed: Int32 = 2
private let kPurchasing: Int32 = 3
private let kFailed: Int32 = 4
private let kPending: Int32 = 5

private final class RetroStoreModel {
    static let shared = RetroStoreModel()

    private let lock = NSLock()
    private var productId = ""
    private var product: Product?
    private var stateValue: Int32 = kLoading
    private var priceValue = ""
    private var expiresValue: Double = 0
    private var errorValue = ""
    private var listener: Task<Void, Never>?

    var state: Int32 { lock.lock(); defer { lock.unlock() }; return stateValue }
    var price: String { lock.lock(); defer { lock.unlock() }; return priceValue }
    var expires: Double { lock.lock(); defer { lock.unlock() }; return expiresValue }
    var error: String { lock.lock(); defer { lock.unlock() }; return errorValue }

    private func apply(state: Int32? = nil, price: String? = nil, expires: Double? = nil, error: String? = nil) {
        lock.lock()
        if let s = state { stateValue = s }
        if let p = price { priceValue = p }
        if let e = expires { expiresValue = e }
        if let m = error { errorValue = m }
        lock.unlock()
    }

    func start(_ id: String) {
        productId = id
        if listener == nil {
            // Renewals, refunds and purchases made on other devices arrive here while the game runs.
            listener = Task.detached { [weak self] in
                for await update in Transaction.updates {
                    if case .verified(let transaction) = update {
                        await transaction.finish()
                    }
                    await self?.refresh()
                }
            }
        }
        Task { [weak self] in
            guard let self = self else { return }
            do {
                let products = try await Product.products(for: [id])
                if let p = products.first {
                    self.product = p
                    self.apply(price: p.displayPrice)
                }
            } catch {
                self.apply(error: error.localizedDescription)
            }
            await self.refresh()
        }
    }

    func refresh() async {
        var active = false
        var until: Double = 0
        for await result in Transaction.currentEntitlements {
            guard case .verified(let t) = result, t.productID == productId, t.revocationDate == nil else { continue }
            if let end = t.expirationDate {
                if end > Date() {
                    active = true
                    until = max(until, end.timeIntervalSince1970)
                }
            } else {
                active = true
            }
        }
        let current = state
        if current == kPurchasing && !active { return } // a purchase in progress decides the state
        apply(state: active ? kSubscribed : kNotSubscribed, expires: until)
    }

    func buy() {
        guard let p = product else {
            apply(state: kFailed, error: "The App Store didn't return the subscription. Check your connection and try again.")
            return
        }
        apply(state: kPurchasing, error: "")
        Task { [weak self] in
            guard let self = self else { return }
            do {
                let result = try await p.purchase()
                switch result {
                case .success(let verification):
                    if case .verified(let t) = verification {
                        await t.finish()
                        self.apply(state: kNotSubscribed)
                        await self.refresh()
                    } else {
                        self.apply(state: kFailed, error: "The purchase couldn't be verified.")
                    }
                case .pending:
                    self.apply(state: kPending)
                case .userCancelled:
                    self.apply(state: kNotSubscribed)
                    await self.refresh()
                @unknown default:
                    self.apply(state: kNotSubscribed)
                    await self.refresh()
                }
            } catch {
                self.apply(state: kFailed, error: error.localizedDescription)
            }
        }
    }

    func restore() {
        apply(state: kLoading, error: "")
        Task { [weak self] in
            guard let self = self else { return }
            do {
                try await AppStore.sync()
            } catch {
                self.apply(error: error.localizedDescription)
            }
            await self.refresh()
        }
    }
}

private func retroCopy(_ s: String) -> UnsafeMutablePointer<CChar>? {
    return strdup(s) // freed by the C# marshaller
}

@_cdecl("RetroStore_Start")
public func RetroStore_Start(_ productId: UnsafePointer<CChar>?) {
    guard let productId = productId else { return }
    RetroStoreModel.shared.start(String(cString: productId))
}

@_cdecl("RetroStore_State")
public func RetroStore_State() -> Int32 {
    return RetroStoreModel.shared.state
}

@_cdecl("RetroStore_Price")
public func RetroStore_Price() -> UnsafeMutablePointer<CChar>? {
    return retroCopy(RetroStoreModel.shared.price)
}

@_cdecl("RetroStore_Error")
public func RetroStore_Error() -> UnsafeMutablePointer<CChar>? {
    return retroCopy(RetroStoreModel.shared.error)
}

@_cdecl("RetroStore_Expires")
public func RetroStore_Expires() -> Double {
    return RetroStoreModel.shared.expires
}

@_cdecl("RetroStore_Buy")
public func RetroStore_Buy() {
    RetroStoreModel.shared.buy()
}

@_cdecl("RetroStore_Restore")
public func RetroStore_Restore() {
    RetroStoreModel.shared.restore()
}

@_cdecl("RetroStore_Refresh")
public func RetroStore_Refresh() {
    Task { await RetroStoreModel.shared.refresh() }
}
