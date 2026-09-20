namespace TakeAuction.Api.Features.Chat;

public sealed class StaticChatKnowledgeBase : IChatKnowledgeBase
{
    public IReadOnlyList<ChatKnowledgeEntry> Entries { get; } =
    [
        new(
            "bidding",
            ["teklif", "teklif vermek", "minimum artış", "limit", "maksimum teklif", "proxy", "kim kazanır", "kazanan"],
            ["bid", "bidding", "minimum increment", "limit", "maximum bid", "proxy", "winner", "who wins"],
            "Bir lotta teklif vermek için oturum açın ve lot sayfasındaki teklif alanına ödeyebileceğiniz en yüksek tutarı yazın. Limitiniz diğer kullanıcılara gösterilmez; sistem, minimum artış kuralına uyarak sizi önde tutmak için yalnız gerektiği kadar otomatik teklif verir. Süre dolduğunda en yüksek teklif kazanır.",
            "Sign in, open a lot, and enter the highest amount you are willing to pay. Your limit stays private; the system raises only as much as needed, following the lot's minimum increment. The highest bid wins when time expires.",
            new("Nasıl çalışır", "/#how-it-works"),
            new("How it works", "/#how-it-works"),
            ["Minimum artış nedir?", "Limitim diğer kullanıcılara görünür mü?", "Açık artırma ne zaman kapanır?"],
            ["What is the minimum increment?", "Can other bidders see my limit?", "When does an auction close?"]),
        new(
            "auctions",
            ["açık artırma", "müzayede", "lot", "parça", "arama", "filtre", "canlı", "planlandı", "sona erdi"],
            ["auction", "lot", "item", "search", "filter", "live", "scheduled", "ended"],
            "Açık artırma salonunda tüm lotları arayabilir; canlı, planlanmış veya sona ermiş durumuna göre filtreleyebilirsiniz. Her kart güncel fiyatı, durumu ve kalan süreyi gösterir.",
            "In the auction hall you can search all lots and filter them as live, scheduled, or ended. Each card shows its current price, status, and remaining time.",
            new("Açık artırmalar", "/auctions"),
            new("Auctions", "/auctions"),
            ["Teklif nasıl verilir?", "Bir lotu nasıl takip ederim?", "Canlı fiyatlar nasıl güncellenir?"],
            ["How do I place a bid?", "How do I watch a lot?", "How do live prices update?"]),
        new(
            "accounts",
            ["kayıt", "hesap", "oturum", "giriş", "alıcı", "satıcı", "rol", "şifre"],
            ["register", "account", "sign in", "login", "buyer", "seller", "role", "password"],
            "Kayıt olurken alıcı veya satıcı rolünü seçersiniz. Alıcılar teklif verebilir; satıcılar kendi lotlarını yayımlayabilir. Mevcut hesabınızla giriş sayfasından oturum açabilirsiniz.",
            "Choose a buyer or seller role when registering. Buyers can bid, while sellers can publish their own lots. Existing members can sign in from the login page.",
            new("Hesap aç", "/register"),
            new("Create an account", "/register"),
            ["Alıcılar neler yapabilir?", "Satıcılar neler yapabilir?", "Teklif nasıl verilir?"],
            ["What can buyers do?", "What can sellers do?", "How do I place a bid?"]),
        new(
            "selling",
            ["ilan", "satış", "satıcı", "yayınla", "açık artırma oluştur", "lot oluştur", "başlangıç fiyatı", "bitiş zamanı", "görsel yükle"],
            ["listing", "sell", "seller", "publish", "create auction", "create lot", "starting price", "end time", "upload image"],
            "Satıcı hesabıyla yeni ilan sayfasından başlık, açıklama, başlangıç fiyatı, minimum artış ve zaman aralığını belirleyebilirsiniz. JPEG, PNG, WebP veya AVIF görsel isteğe bağlıdır. Açık artırma 5 dakika ile 30 gün arasında sürmelidir.",
            "With a seller account, use the new listing page to set the title, description, starting price, minimum increment, and schedule. A JPEG, PNG, WebP, or AVIF image is optional. Auctions may run from 5 minutes to 30 days.",
            new("Yeni ilan", "/auctions/new"),
            new("New listing", "/auctions/new"),
            ["Hangi görselleri yükleyebilirim?", "Minimum artışı kim belirler?", "İlanımı geri çekebilir miyim?"],
            ["Which images can I upload?", "Who sets the minimum increment?", "Can I withdraw my listing?"]),
        new(
            "watchlist",
            ["takip", "takip listesi", "izle", "hatırlat", "kapanış bildirimi"],
            ["watch", "watchlist", "follow", "remind", "closing notification"],
            "Oturum açtıktan sonra bir lot sayfasındaki “Takibe alın” düğmesini kullanabilirsiniz. Takip ettiğiniz lotlar kişisel listenizde görünür ve kapanıştan yaklaşık beş dakika önce bildirim alırsınız.",
            "After signing in, use “Watch this lot” on a lot page. Watched lots appear in your personal list, and you receive a notification about five minutes before closing.",
            new("Takip listem", "/watchlist"),
            new("My watchlist", "/watchlist"),
            ["Bildirimler nasıl çalışır?", "Bir lotu takipten nasıl çıkarırım?", "Teklif nasıl verilir?"],
            ["How do notifications work?", "How do I stop watching a lot?", "How do I place a bid?"]),
        new(
            "realtime",
            ["anlık", "gerçek zamanlı", "canlı güncelleme", "yenileme", "signalr", "son saniye", "uzatma"],
            ["real time", "realtime", "live update", "refresh", "signalr", "last second", "extension"],
            "Kabul edilen teklifler SignalR üzerinden salondaki ekranlara anında iletilir; sayfayı yenilemeniz gerekmez. Son saniyelerde gelen geçerli teklifler sıraya alınır ve yapılandırılmış anti-sniping kuralı gerektiğinde bitiş zamanını uzatabilir.",
            "Accepted bids are sent to every screen through SignalR, so no refresh is needed. Valid last-second bids are ordered correctly, and the configured anti-sniping rule may extend the closing time when required.",
            new("Nasıl çalışır", "/#how-it-works"),
            new("How it works", "/#how-it-works"),
            ["Teklifler çakışırsa ne olur?", "Açık artırma ne zaman kapanır?", "Bildirimler nasıl çalışır?"],
            ["What if bids arrive together?", "When does an auction close?", "How do notifications work?"]),
        new(
            "notifications",
            ["bildirim", "kazandım", "satıldı", "teklif geçildi", "outbid", "zil"],
            ["notification", "won", "sold", "outbid", "bell", "alert"],
            "Oturum açmış kullanıcılar bildirim zilinde geçilen teklifler, kazanılan veya satılan lotlar ve yaklaşan kapanışlar gibi kişisel gelişmeleri görür. Bu bilgiler yalnız kendi oturumunuzda gösterilir.",
            "Signed-in members see personal updates in the notification bell, including outbid events, won or sold lots, and approaching closings. These details are shown only in your own session.",
            new("Açık artırmalar", "/auctions"),
            new("Auctions", "/auctions"),
            ["Bir lotu nasıl takip ederim?", "Canlı fiyatlar nasıl güncellenir?", "Teklif nasıl verilir?"],
            ["How do I watch a lot?", "How do live prices update?", "How do I place a bid?"])
    ];
}
