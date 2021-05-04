# TradingView Alerts Trader
## Description
This is a simple ASP.Net Core application to use any alerts from TradingView to create market buy/sell orders in Binance.

## Simple buy/sells
Below is a screenshot of a TradingView alert. The alert needs to point to where the site will be hosted (The source code will need to be published to a host e.g. Azure). The URL will be the host site '/Trading' e.g. https://<HOSTSITE>/Trading
![Screenshot](https://github.com/Hallupa/TradingViewAlertsTrader/blob/main/Doc/Images/TradingView.png)

To initiate the trade, the alert message will need to contain details of the trade. This will be in the format:
Step,Symbol,BUY/SELL,Amount

e.g. 1,BTCUSDT,BUY,0.1

### Amount
The Amount can either be:
- An amount of the asset being bought/sold
- Dollar amount e.g. $500 (This will be based on USDT)
- Percentage amount e.g. 10%. This will be the percentage of the total asset balance when being sold. When buying, will be a percentage of the asset balance of the asset being sold to buy the new asset.

### Step
A step can also be provided to ensure previous alerts are triggered before the current alert is processed to allow more complex alerts.
If the step is '1' then the alert will always be processed.
If it is e.g. '5', then '4' will have to be triggered first. If '5' is triggered, '6' will be triggered next. Any other number except '1' won't be triggered.

#### Step example
Below shows where a market has a false breakdown. The market wicks down - if a trade had it's stop loss around here it would have been triggered. Afterwards, the market rises back to it's previous range.
![Screenshot](https://github.com/Hallupa/TradingViewAlertsTrader/blob/main/Doc/Images/TradingView3.png)

What would be better would be to have a way of detecting a breakdown, a pullback to resistance (Which was previous support) then a further drop such as this:
![Screenshot](https://github.com/Hallupa/TradingViewAlertsTrader/blob/main/Doc/Images/TradingView1.png)

This can be done using steps such as this:
![Screenshot](https://github.com/Hallupa/TradingViewAlertsTrader/blob/main/Doc/Images/TradingView2.png)

In this example the alerts would be (In order from top to bottom):
1,BTCUSDT
2,BTCUSDT
3,BTCUSDT,SELL,100%

Note that the alert doesn't need to have a buy/sell event, it can just be a step and a market.
When setting these alerts up consider carefully how they are setup, e.g. should they trigger just once, on prices moving up, down, etc. Alerts in TradingView can be triggered multiple times - this application can't distinguish between them so could cause the same event to happen multiple times.

# Disclaimer
This project is a work in progress and experimental. If you use this, use it casefully. I can not be held responsible for any bugs or mistakes when using it or any losses that an occur.
