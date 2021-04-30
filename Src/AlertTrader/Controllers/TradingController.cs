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

        public TradingController(ILogger<TradingController> logger, IConfiguration configuration)
        {
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> Post()
        {
            var rawRequestBody = await GetRawBodyAsync(Request);

            var headers = string.Join(',', Request.Headers.Select(x => $"{x.Key} = {x.Value}"));

            _lastResult = rawRequestBody + $"\n{headers}";

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

                WebCallResult<BinanceAveragePrice> avPrice;
                if (ticker.EndsWith("USDT"))
                {
                    avPrice = await client.Spot.Market.GetCurrentAvgPriceAsync(ticker);
                    var accountInfo = await client.General.GetAccountInfoAsync();

                    if (ev.ToUpper() == "BUY")
                    {
                        var quantity = GetAmountToBuy(amountStr, accountInfo, avPrice);

                        if (quantity <= 0M) return Ok();


                        var res = await client.Spot.Order.PlaceOrderAsync(ticker, OrderSide.Buy, OrderType.Market, quantity);
                    }
                    else if (ev.ToUpper() == "SELL")
                    {
                        var quantity = GetAmountToSell(amountStr, ticker, accountInfo, avPrice);
                        if (quantity <= 0M) return Ok();

                        var res = await client.Spot.Order.PlaceOrderAsync(ticker, OrderSide.Sell, OrderType.Market, quantity);
                    }
                }
            }
            catch (Exception ex)
            {
                _lastResult += $"\n{ex}";
            }

            return Ok();
        }

        private static decimal GetAmountToBuy(string amountStr, WebCallResult<BinanceAccountInfo> accountInfo, WebCallResult<BinanceAveragePrice> avPrice)
        {
            decimal quantity = 0;
            if (amountStr.Contains("$"))
            {
                var amountUsdt = decimal.Parse(amountStr.Replace("$", ""));
                var usdtFreeBalance = accountInfo.Data.Balances.First(b => b.Asset == "USDT").Free;

                if (usdtFreeBalance < amountUsdt)
                {
                    amountUsdt = usdtFreeBalance;
                }

                quantity = amountUsdt / avPrice.Data.Price;
            }

            return quantity;
        }

        private decimal  GetAmountToSell(string amountStr, string ticker, WebCallResult<BinanceAccountInfo> accountInfo, WebCallResult<BinanceAveragePrice> avPrice)
        {
            decimal quantity = 0;
            if (amountStr.Contains("$"))
            {
                var asset = ticker.Replace("USDT", "");
                var amountUsdt = decimal.Parse(amountStr.Replace("$", ""));
                var freeBalance = accountInfo.Data.Balances.First(b => b.Asset == asset).Free;
                if (freeBalance <= 0M) return quantity;

                var totalBalanceInUsdt = avPrice.Data.Price * freeBalance;

                if (totalBalanceInUsdt < amountUsdt)
                {
                    amountUsdt = totalBalanceInUsdt;
                }

                quantity = amountUsdt / avPrice.Data.Price;
            }

            return quantity;
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