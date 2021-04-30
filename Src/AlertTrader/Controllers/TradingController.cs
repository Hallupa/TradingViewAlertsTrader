using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Binance.Net;
using Binance.Net.Enums;
using Binance.Net.Objects.Spot;
using Binance.Net.Objects.Spot.MarketData;
using Binance.Net.Objects.Spot.SpotData;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Objects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AlertTrader.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TradingController : ControllerBase
    {
        private static string _lastResult;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TradingController> _logger;

        private readonly string[] TradingViewIps = 
        {
            "52.89.214.238", "34.212.75.30", "54.218.53.128", "52.32.178.7"
        };

        public TradingController(ILogger<TradingController> logger, IConfiguration configuration)
        {
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> Post()
        {
            var ip = Request.HttpContext.Connection.RemoteIpAddress.ToString();

            _logger.LogInformation($"Request received from {ip}");

            if (!TradingViewIps.Contains(ip))
            {
                _logger.LogError("Request not received from TradingView");
                return Ok();
            }

            var rawRequestBody = await GetRawBodyAsync(Request);

            var headers = string.Join(',', Request.Headers.Select(x => $"{x.Key} = {x.Value}"));

            _lastResult = rawRequestBody + $"\n{headers}";

            _logger.LogInformation($"Request received:\n{rawRequestBody}\n{headers}");


            try
            {
                var reqDetails = rawRequestBody.Split(',');
                var ticker = reqDetails[0];
                var ev = reqDetails[1];
                var amountStr = reqDetails[2];

                var apiKey = _configuration["BinanceAPIKey"];
                var secretKey = _configuration["BinanceSecretKey"];

                var client = new BinanceClient(new BinanceClientOptions
                {
                    ApiCredentials = new ApiCredentials(apiKey, secretKey)
                });

                var avPrice = await client.Spot.Market.GetCurrentAvgPriceAsync(ticker);
                var accountInfo = await client.General.GetAccountInfoAsync();

                if (ev.ToUpper() == "BUY")
                {
                    var quantity = GetAmountToBuy(amountStr, ticker, accountInfo, avPrice, client);

                    if (quantity <= 0M) return Ok();

                    _logger.LogInformation($"BUYING: {quantity} ({amountStr}) {ticker} @ {avPrice.Data.Price}");
                    var res = await client.Spot.Order.PlaceOrderAsync(ticker, OrderSide.Buy, OrderType.Market, quantity);
                    _logger.LogInformation($"Buy result: Success: {res.Success}");
                }
                else if (ev.ToUpper() == "SELL")
                {
                    var quantity = GetAmountToSell(amountStr, ticker, accountInfo, avPrice, client);
                    if (quantity <= 0M) return Ok();

                    _logger.LogInformation($"SELLING: {quantity} ({amountStr}) {ticker} @ {avPrice.Data.Price}");
                    var res = await client.Spot.Order.PlaceOrderAsync(ticker, OrderSide.Sell, OrderType.Market, quantity);
                    _logger.LogInformation($"Sell result: Success: {res.Success}");
                }
            }
            catch (Exception ex)
            {
                _lastResult += $"\n{ex}";

                _logger.LogError($"Exception: {ex}");
            }

            return Ok();
        }

        private static decimal GetAmountToBuy(
            string amountStr,
            string ticker,
            WebCallResult<BinanceAccountInfo> accountInfo,
            WebCallResult<BinanceAveragePrice> avPrice,
            BinanceClient client)
        {
            // Get selling asset and selling asset balance
            var sellingAsset = ticker.Substring(ticker.Length - 3, 3);
            var sellingAssetBalance = accountInfo.Data.Balances.FirstOrDefault(b => b.Asset == sellingAsset);
            if (sellingAssetBalance == null)
            {
                sellingAsset = ticker.Substring(ticker.Length - 4, 4);
                sellingAssetBalance = accountInfo.Data.Balances.FirstOrDefault(b => b.Asset == sellingAsset);
            }

            if (sellingAssetBalance == null || sellingAssetBalance.Free <= 0M) return 0M;
            var amountToBuy = decimal.Parse(amountStr.Replace("%", "").Replace("$", ""));

            // % of available balance
            if (amountStr.Contains("%"))
            {
                // Get amount to buy. E.g. ETHBTC - get amount of ETH that can be bought with the % of BTC
                var sellingAssetAmount = sellingAssetBalance.Free * (amountToBuy / 100.0M);

                // Convert the selling asset amount to the buying asset amount. E.g. using 1 BTC to buy 20 ETH
                return sellingAssetAmount / avPrice.Data.Price;
            }

            if (amountStr.Contains("$"))
            {
                // Paired with USDT. E.g. BTCUSDT  56000
                if (ticker.EndsWith("USDT"))
                {
                    // Convert $ amount to asset amount
                    amountToBuy = amountToBuy / avPrice.Data.Price;
                }
                else
                {
                    // Get USDT price. e.g. BTCETH so get BTCUSDT
                    var buyingAssetUsdtPrice = client.Spot.Market.GetCurrentAvgPrice($"{ticker.Replace(sellingAsset, "")}USDT");

                    // Get asset amount
                    amountToBuy = amountToBuy / buyingAssetUsdtPrice.Data.Price;
                }
            }

            if (sellingAssetBalance.Free < amountToBuy)
            {
                amountToBuy = sellingAssetBalance.Free;
            }

            return amountToBuy;
        }

        private decimal GetAmountToSell(
            string amountStr,
            string ticker,
            WebCallResult<BinanceAccountInfo> accountInfo,
            WebCallResult<BinanceAveragePrice> avPrice,
            BinanceClient client)
        {
            // Get buying asset and selling asset balance
            var sellingAsset = ticker.Substring(0, 3);
            var sellingAssetBalance = accountInfo.Data.Balances.FirstOrDefault(b => b.Asset == sellingAsset);
            if (sellingAssetBalance == null)
            {
                sellingAsset = ticker.Substring(0, 4);
                sellingAssetBalance = accountInfo.Data.Balances.FirstOrDefault(b => b.Asset == sellingAsset);
            }

            if (sellingAssetBalance == null || sellingAssetBalance.Free <= 0M) return 0M;
            var amountToSell = decimal.Parse(amountStr.Replace("%", "").Replace("$", ""));

            // % of available balance
            if (amountStr.Contains("%"))
            {
                return sellingAssetBalance.Free * (amountToSell / 100.0M);
            }

            if (amountStr.Contains("$"))
            {
                // Paired with USDT. E.g. BTCUSDT  56000
                if (ticker.EndsWith("USDT"))
                {
                    // Convert $ amount to asset amount
                    amountToSell = amountToSell / avPrice.Data.Price;
                }
                else
                {
                    // Get USDT price. e.g. BTCETH so get BTCUSDT
                    var sellingAssetUsdtPrice = client.Spot.Market.GetCurrentAvgPrice($"{sellingAsset}USDT");

                    // Get asset amount
                    amountToSell = amountToSell / sellingAssetUsdtPrice.Data.Price;
                }
            }

            if (sellingAssetBalance.Free < amountToSell)
            {
                amountToSell = sellingAssetBalance.Free;
            }

            return amountToSell;
        }

        [HttpGet]
        public string Get()
        {
            return $"Last request: {_lastResult}";
        }

        public static async Task<string> GetRawBodyAsync(
            HttpRequest request,
            Encoding encoding = null)
        {
            if (!request.Body.CanSeek)
            {
                // We only do this if the stream isn't *already* seekable,
                // as EnableBuffering will create a new stream instance
                // each time it's called
                request.EnableBuffering();
            }

            request.Body.Position = 0;

            var reader = new StreamReader(request.Body, encoding ?? Encoding.UTF8);

            var body = await reader.ReadToEndAsync().ConfigureAwait(false);

            request.Body.Position = 0;

            return body;
        }
    }
}