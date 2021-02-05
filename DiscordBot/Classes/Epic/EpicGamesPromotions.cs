using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace DiscordBot.Classes.Epic
{
    public class EpicGamesPromotions
    {
        [JsonProperty("data")]
        public EpicData Data { get; set; }

        [JsonProperty("extensions")]
        public JObject Extensions { get; set; }
    }
    public class EpicData
    {
        public EpicCatalog Catalog { get; set; }
    }
    public class EpicCatalog
    {
        [JsonProperty("searchStore")]
        public EpicSearchStore SearchStore { get; set; }
    }
    public class EpicSearchStore
    {
        [JsonProperty("elements")]
        public EpicStoreElement[] Elements { get; set; }
        [JsonProperty("paging")]
        public EpicStorePaging Paging { get; set; }
    }
    public class EpicStorePaging
    {
        [JsonProperty("count")]
        public int Count { get; set; }
        [JsonProperty("total")]
        public int Total { get; set; }
    }
    [DebuggerDisplay("{Title,nq}")]
    public class EpicStoreElement
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("namespace")]
        public string Namespace { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("effectiveDate")]
        public DateTime EffectiveDate { get; set; }

        [JsonProperty("productSlug")]
        public string ProductSlug { get; set; }

        [JsonProperty("urlSlug")]
        public string UrlSlug { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("keyImages")]
        public EpicElementImage[] KeyImages { get; set; }

        [JsonProperty("items")]
        public EpicItem[] Items { get; set; }

        [JsonProperty("customAttributes")]
        public EpicAttribute[] CustomAttributes { get; set; }

        [JsonProperty("categories")]
        public EpicCategory[] Categories { get; set; }

        [JsonProperty("tags")]
        public EpicTag[] Tags { get; set; }

        [JsonProperty("price")]
        public EpicElementPrice Price { get; set; }

    }
    [DebuggerDisplay("{Type,nq} {Url,nq}")]
    public class EpicElementImage
    {
        [JsonProperty("type")]
        public EpicImageType Type { get; set; }
        [JsonProperty("url")]
        public Uri Url { get; set; }
    }
    public enum EpicImageType
    {
        None,
        OfferImageWide,
        OfferImageTall,
        Thumbnail,
        DieselStoreFrontWide,
        DieselStoreFrontTall,
        CodeRedemption_340x440,
        VaultClosed,
        DieselGameBoxLogo
    }
    [DebuggerDisplay("{Name}")]
    public class EpicSeller
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("name")]
        public string Name { get; set; }
    }
    [DebuggerDisplay("{Namespace}")]
    public class EpicItem
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("namespace")]
        public string Namespace { get; set; }
    }
    [DebuggerDisplay("{Key}: {Value}")]
    public class EpicAttribute
    {
        [JsonProperty("key")]
        public string Key { get; set; }
        [JsonProperty("value")]
        public string Value { get; set; }
    }
    [DebuggerDisplay("{Path}")]
    public class EpicCategory
    {
        [JsonProperty("path")]
        public string Path { get; set; }
    }
    [DebuggerDisplay("{Id}")]
    public class EpicTag
    {
        [JsonProperty("id")]
        public string Id { get; set; }
    }
    public class EpicElementPrice
    {
        [JsonProperty("totalPrice")]
        public EpicTotalPrice TotalPrice { get; set; }

        [JsonProperty("lineOffers")]
        public JArray LineOffers { get; set; }
    }
    public class EpicTotalPrice
    {
        [JsonProperty("discountPrice")]
        public int DiscountPrice { get; set; }

        [JsonProperty("originalPrice")]
        public int OriginalPrice { get; set; }

        [JsonProperty("voucherDiscount")]
        public int VoucherDiscount { get; set; }

        [JsonProperty("discount")]
        public int Discount { get; set; }

        [JsonProperty("currencyCode")]
        public string CurrencyCode { get; set; }

        [JsonProperty("currencyInfo")]
        public EpicCurrencyInfo CurrencyInfo { get; set; }

        [JsonProperty("fmtPrice")]
        public EpicFormattedPrice FormattedPrice { get; set; }
    }
    public class EpicFormattedPrice 
    {
        [JsonProperty("discountPrice")]
        public string DiscountPrice { get; set; }

        [JsonProperty("originalPrice")]
        public string OriginalPrice { get; set; }
    }
    public class EpicCurrencyInfo
    {
        [JsonProperty("decimals")]
        public int Decimals { get; set; }
    }
}
