namespace TakeAuction.Api.Common.Persistence.Seeding;

public sealed record ShowcaseItem(
    string Slug,
    string Title,
    string Description,
    decimal StartingPrice,
    decimal MinimumBidIncrement,
    int DurationHours);

public static class ShowcaseCatalog
{
    public static readonly IReadOnlyList<ShowcaseItem> Items =
    [
        new(
            "bmw-m5",
            "BMW M5 G90 — 2024 Sıfır Ayarında",
            "2024 model BMW M5 G90, teslim kilometresinde. 4.4 litre çift turbo V8 hibrit motor, "
            + "karbon dış paket ve eksiksiz servis geçmişi. Tek sahipli, belgeli. "
            + "Ekspertiz raporu kazanan teklif sahibiyle paylaşılır.",
            145_000m,
            1_000m,
            72),
        new(
            "canon-5d",
            "Canon EOS 5D Mark IV — Stüdyo Seti",
            "Stüdyo bakımlı Canon EOS 5D Mark IV gövde, 40.000 deklanşörün altında. "
            + "İki batarya, çift kart yapılandırması ve orijinal kutusuyla birlikte. "
            + "Sensörü yetkili servis tarafından yakın zamanda temizlenip kalibre edildi.",
            1_850m,
            50m,
            48),
        new(
            "vintage-sofa",
            "Vintage Deri Chesterfield Koltuk",
            "Onlarca yıllık kullanımla derin bir patina kazanmış mid-century deri Chesterfield. "
            + "Elle bağlanmış yaylar, masif sert ahşap iskelet ve orijinal pirinç tekerlekler. "
            + "Yeniden döşeme gerektirmez; deri esnekliğini ve yapısal sağlamlığını koruyor.",
            4_200m,
            100m,
            96),
        new(
            "satellite",
            "Referans Serisi Satellite Hoparlör",
            "Kapalı kabinli referans serisi satellite hoparlör, fırçalanmış alüminyum ön baffle ve "
            + "yüksek yoğunluklu MDF gövde. Arka panelde çift terminal bağlantısı, bi-wiring destekli. "
            + "Stüdyo referans sisteminden çıkma; sürücüler ve kabin kusursuz durumda.",
            8_500m,
            250m,
            120),
        new(
            "fridge",
            "Ankastre Panel Uyumlu Buzdolabı",
            "Fırçalanmış paslanmaz kaplamalı, panel uyumlu kolon tipi buzdolabı. "
            + "Çift bölgeli iklim kontrolü ve tamamen modüler iç düzen. "
            + "Bir mimarlık showroom'undan çıkma teşhir ünitesi; hiç kullanılmadı, üretici garantisi devam ediyor.",
            3_400m,
            100m,
            36)
    ];
}
