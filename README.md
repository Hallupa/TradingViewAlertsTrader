# TradingView Alerts Trader
## Description
This is a simple ASP.Net Core application to use any alerts from TradingView to create market buy/sell orders in Binance.

## Setting up alerts
Below is a screenshot of a TradingView alert. The alert needs to point to where the site will be hosted (The source code will need to be published to a host e.g. Azure). The URL will be the host site '\Trading' e.g. https://hostingsite.com/Trading 
![Screenshot](https://github.com/Hallupa/TradingViewAlertsTrader/blob/main/Doc/Images/TradingView.png)

To initiate the trade, the alert message will need to contain details of the trade. This will be in the format:
Symbol,Amount,BUY/SELL

e.g. BTCUSDT,0.1,BUY

### Amount
The Amount can either be:
- An amount of the asset being bought/sold
- Dollar amount e.g. $500
- Percentage amount e.g. 10%. This will be the percentage of the total asset balance when being sold. When buying, will be a percentage of the asset balance of the asset being sold to buy the new asset.

### Step (Optional)
A step can also be provided to ensure previous alerts are triggered before the current alert is processed.

Symbol,Amount,BUY/SELL,Step
e.g. BTCUSDT,0.1,BUY,3
In this case alerts with step 1 and 2 must be triggered first otherwise this alert will be ignored.
