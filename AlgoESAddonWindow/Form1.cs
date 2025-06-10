using System;
using System.Collections.ObjectModel;
using System.Windows.Forms;
using IBApi;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;
//using TWSLib;
using System.Diagnostics;
using System.Threading;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Data;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Text;
using System.Collections.Concurrent;

namespace AlgoESAddonWindow
{
    public static class ControlExtensions
    {
        public static Task InvokeAsync(this Control control, Action action)
        {
            var tcs = new TaskCompletionSource<object>();

            void SafeAction()
            {
                try
                {
                    action();
                    tcs.SetResult(null);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }

            if (control.InvokeRequired)
            {
                control.BeginInvoke((MethodInvoker)(() => SafeAction()));
            }
            else
            {
                SafeAction();
            }

            return tcs.Task;
        }
    }
    public partial class formAlgoES : Form, EWrapper
    {
        public EClientSocket clientSocket;
        public int requestId = 0;
        public readonly EReaderSignal signal;
        public Contract contractNQJun25;
        public Contract contractNQSep25;
        public Order sellOrder, buyOrder;
        public EReader reader;
        public int requestIdAAPL = 1; // Unique request ID for ESZ24
        public int requestIdGBP = 2;  // Unique request ID for ESH25
        public double triger_price1 = 0;
        private static bool _boolStartTick = false;
        private static bool _boolStartTrading = false;
        // Field to store order status
        public string orderStatusMessage;
        private static bool _boolStartAutoExposure = false;
        private static bool _boolShowTrades = false;
        public int orderIdchange;
        public String orderstatuschange;
        OrderCancel orderCancel;
        // Field to store submitted price
        public double orderPrice;
        private readonly ConcurrentDictionary<int, string> orderStatuses = new();
        private Dictionary<int, double> submittedPrices; // Store submitted prices
        public const double POINT_THRESHOLD = 1; // Threshold for cancellation
        public double pipValue ;
        public List<Dictionary<string, string>> allOrders = new List<Dictionary<string, string>>();
        private Dictionary<int, Dictionary<string, string>> ordersDict = new();
        public formAlgoES()
        {
            InitializeComponent();

            signal = new EReaderMonitorSignal();

            clientSocket = new EClientSocket(this, signal);

            reader = new EReader(clientSocket, signal);

            contractNQJun25 = new Contract();
            contractNQSep25 = new Contract();

            sellOrder = new Order();
            buyOrder = new Order();

            orderStatusMessage = string.Empty;
            submittedPrices = new Dictionary<int, double>();
            panelLongTrade.AutoScroll = true;
            panelLongTrade.AutoSize = false;

            panelShortTrade.AutoScroll = true;
            panelShortTrade.AutoSize = false;
            orderIdchange = 0;
            orderstatuschange = "";
            orderCancel = new OrderCancel();
            pipValue = 0;


        }
        private void btnStartTick_Click(object sender, EventArgs e)
        {
            try
            {
                clientSocket.eConnect("127.0.0.1", 7497, 0);

                if (!clientSocket.IsConnected())
                {
                    MessageBox.Show("Connection failed.");
                    return;
                }

                var reader = new EReader(clientSocket, signal);
                reader.Start();

                new Thread(() =>
                {
                    while (clientSocket.IsConnected())
                    {
                        signal.waitForSignal();
                        reader.processMsgs();
                    }
                })
                { IsBackground = true }.Start();

                MessageBox.Show("Connected");

                //Contract contractESJun25 = new Contract
                //{
                //    Symbol = "ES",
                //    SecType = "FUT",
                //    Exchange = "CME",
                //    Currency = "USD",
                //    LastTradeDateOrContractMonth = "202506",
                //    Multiplier = "50"
                //};

                //Contract contractESSep25 = new Contract
                //{
                //    Symbol = "ES",
                //    SecType = "FUT",
                //    Exchange = "CME",
                //    Currency = "USD",
                //    LastTradeDateOrContractMonth = "202509",
                //    Multiplier = "50"
                //};

                contractNQJun25 = new Contract
                {
                    Symbol = "NQ",
                    SecType = "FUT",
                    Exchange = "CME",
                    Currency = "USD",
                    LastTradeDateOrContractMonth = "202506",
                    Multiplier = "20"
                };

                contractNQSep25 = new Contract
                {
                    Symbol = "NQ",
                    SecType = "FUT",
                    Exchange = "CME",
                    Currency = "USD",
                    LastTradeDateOrContractMonth = "202509",
                    Multiplier = "20"
                };

                clientSocket.reqMarketDataType(3);
                //clientSocket.reqMktData(1001, contractESJun25, "", false, false, null);
                //clientSocket.reqMktData(1002, contractESSep25, "", false, false, null);
                clientSocket.reqMktData(1003, contractNQJun25, "", false, false, null);
                clientSocket.reqMktData(1004, contractNQSep25, "", false, false, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during connection: {ex.Message}");
            }

        }

        // EWrapper interface implementations
        public void tickPrice(int reqId, int field, double price, TickAttrib attribs)
        {
            //if (reqId == 1)
            //{
            //    // Use Invoke to update the UI thread
            //    if (textCurrentLongUpper.InvokeRequired)
            //    {
            //        textCurrentLongUpper.Invoke(new Action(() => textCurrentLongUpper.Text = price.ToString("F4")));
            //        CheckAndCancelOrders( price); // Check for cancellation 

            //    }
            //    else
            //    {
            //        textCurrentLongUpper.Text = price.ToString("F4");
            //        CheckAndCancelOrders( price); // Check for cancellation 
            //    }

            //}

            //if (reqId == 2)
            //{
            //    // Use Invoke to update the UI thread
            //    if (textCurrentShortUpper.InvokeRequired)
            //    {
            //        textCurrentShortUpper.Invoke(new Action(() => textCurrentShortUpper.Text = price.ToString("F4")));
            //        CheckAndCancelOrders( price); // Check for cancellation 
            //    }
            //    else
            //    {
            //        textCurrentShortUpper.Text = price.ToString("F4");
            //        CheckAndCancelOrders( price); // Check for cancellation 
            //    }
            //}

            //textCurrentLongUpper.Text = "No reqID = 1";

            //textCurrentShortUpper.Text = "No reqID = 2";

            string contractName = reqId switch
            {
                1001 => "ES Jun25",
                1002 => "ES Sep25",
                1003 => "NQ Jun25",
                1004 => "NQ Sep25",
                _ => "Other"
            };

            string priceType = field switch
            {
                1 => "Bid",
                2 => "Ask",
                4 => "Last",
                6 => "High",
                7 => "Low",
                9 => "Close",
                _ => $"Field {field}"
            };

            //textLongTradingPane.InvokeAsync(new Action(() =>
            //{
            //    textLongTradingPane.AppendText(
            //        $"{DateTime.Now:HH:mm:ss} {contractName} {priceType}: {price}\n"
            //    );
            //}));
        }

        //// Method to check if you need to cancel the order

        private void CheckAndCancelOrders( double currentPrice)
        {
            foreach (var order in orderStatuses)
            {
                int orderId = order.Key;
                string status = order.Value;

                if (status == "Submitted") // Check if order is still active
                {
                    // Check if current price is within 1 point of submitted price
                    if (submittedPrices.ContainsKey(orderId) &&
                    Math.Abs(currentPrice - submittedPrices[orderId]) > POINT_THRESHOLD * pipValue)
                    {
                        clientSocket.cancelOrder(orderId, orderCancel); // Cancel the order
                        Console.WriteLine($"Order ID {orderId} has been canceled because it was within 1 point of the submitted price.");
                        UpdateAllOrderStatusUI(); // Update UI with all orders' statuses
                    }
                }
            }

        }

        private void ClearLongTradePanel()
        {
            panelLongTrade.Controls.Clear();
        }

        private void ClearShortTradePanel()
        {
            panelShortTrade.Controls.Clear();
        }

        private void btnSetLongBoxParams_Click(object sender, EventArgs e)
        {
        }

        private void btnClosePosition_Click(object sender, EventArgs e)
        {

        }

        private void btnStartTrading_Click(object sender, EventArgs e)
        {
            _boolStartTrading = !_boolStartTrading;

            if (_boolStartTrading == true)
            {
                btnStartTrading.Text = "Stop Trading";
                btnStartTrading.BackColor = Color.Gray;
                btnStartTrading.ForeColor = Color.Black;
            }
            else
            {
                btnStartTrading.Text = "Start Trading";
                btnStartTrading.BackColor = Color.Red;
                btnStartTrading.ForeColor = Color.White;
            }  
        }

        private void btnShowTrades_Click(object sender, EventArgs e)
        {   
            if (_boolStartTrading == true)
            {
                _boolShowTrades = !_boolShowTrades;

                if (_boolShowTrades == true)
                {
                    btnShowTrades.Text = "Show Positions";
                    btnShowTrades.BackColor = Color.Gray;
                    btnShowTrades.ForeColor = Color.Black;

                    _boolShowTrades = true;

                    if (double.TryParse(txtNeutralPrice.Text, out double buyPrice))
                    {
                        // IBKR-specific validation
                        if (buyPrice <= 0)
                        {
                            MessageBox.Show("Price must be greater than zero");
                            _boolShowTrades = false;
                            btnShowTrades.Text = "Show Trades";
                            return;
                        }

                        buyOrder = new Order
                        {
                            Action = "BUY",
                            TotalQuantity = 100,
                            OrderType = "LMT",
                            LmtPrice = buyPrice,
                            Tif = "DAY"
                        };
                        clientSocket.reqContractDetails(1003, contractNQJun25);
                        clientSocket.placeOrder(requestId, contractNQJun25, buyOrder);
                        //orderPrice = double.Parse(textCurrentLongUpperPosition.Text);
                        textLongTradingPane.AppendText(
                            $"Successfully buy order submitted: {requestId}\n"
                        );
                        allOrders.Add(new Dictionary<string, string>
                        {
                            { "OrderId", requestId.ToString()},
                            { "Action", buyOrder.Action.ToString()},
                            { "OrderType", buyOrder.OrderType.ToString() },
                            { "Status", "Inactive" }
                        });
                        requestId++;

                    }
                    else
                    {
                        btnShowTrades.Text = "Show Trades";
                        _boolShowTrades = false;
                        MessageBox.Show("Please input the price for buy");
                    }
                }
                else
                {
                    btnShowTrades.Text = "Show Trades";
                    btnShowTrades.BackColor = Color.Blue;
                    btnShowTrades.ForeColor = Color.White;

                    _boolShowTrades = false;
                }
            }
        }

        private void btnStartAutoExposure_Click(object sender, EventArgs e)
        {
            if (_boolStartTrading == true) {

                _boolStartAutoExposure = !_boolStartAutoExposure;

                if (_boolStartAutoExposure == true)
                {
                    btnStartAutoExposure.Text = "Stop Freeze";
                    btnStartAutoExposure.BackColor = Color.Gray;
                    btnStartAutoExposure.ForeColor = Color.Black;

                    _boolStartAutoExposure = true;

                    if (double.TryParse(txtBackMinusFrontPoints.Text, out double sellPrice))
                    {
                        // IBKR-specific validation
                        if (sellPrice <= 0)
                        {
                            MessageBox.Show("Price must be greater than zero");
                            _boolStartAutoExposure = false;
                            btnStartAutoExposure.Text = "Start Freeze";
                            return;
                        }

                        sellOrder = new Order
                        {
                            Action = "SELL",
                            TotalQuantity = 100,
                            OrderType = "LMT",
                            LmtPrice = sellPrice,
                            Tif = "DAY"
                        };
                        clientSocket.reqContractDetails(1004, contractNQSep25);
                        clientSocket.placeOrder(requestId, contractNQSep25, sellOrder);
                        textLongTradingPane.AppendText(
                            $"Successfully sell order submitted: {requestId}\n"
                        );
                        //orderPrice = double.Parse(textCurrentLongUpperPosition.Text);
                        allOrders.Add(new Dictionary<string, string>
                        {
                            { "OrderId", requestId.ToString()},
                            { "Action", sellOrder.Action.ToString()},
                            { "OrderType", sellOrder.OrderType.ToString() },
                            { "Status", "Inactive" }
                        });
                        requestId++;
                    }
                    else
                    {
                        btnStartAutoExposure.Text = "Start Freeze";
                        _boolShowTrades = false;
                        MessageBox.Show("Please input the price for sell");
                    }
                }
                else
                {
                    btnStartAutoExposure.Text = "Start Freeze";
                    btnStartAutoExposure.BackColor = Color.Red;
                    btnStartAutoExposure.ForeColor = Color.White;

                    _boolStartAutoExposure = false;
                }

                // If you click this btnStartAutoExposure_Click button, you can see order status in textShortTradingPane.Text when you input orderID in txtNeutralPrice.Text
                //try
                //{

                //    // Parse the order ID from the TextBox
                //    if (int.TryParse(txtNeutralPrice.Text, out int orderId))
                //    {
                //        UpdateOrderStatusDisplay(orderId, orderStatuses[orderId]);
                //    }
                //    else
                //    {
                //        MessageBox.Show("Please enter a valid order ID.");
                //    }
                //}
                //catch (Exception ex)
                //{
                //    MessageBox.Show($"Error fetching order status: {ex.Message}");
                //}
            }

        }

        private void btnSetShortBoxParams_Click(object sender, EventArgs e)
        {
        }

        private void btnActivateBox_Click(object sender, EventArgs e)
        {
        }

        private void btnInactivateBox_Click(object sender, EventArgs e)
        {
        }

        private void btnAutoLock_Click(object sender, EventArgs e)
        {
        }

        private void btnNoAutoLock_Click(object sender, EventArgs e)
        {
        }

        private void btnFixedOpen_Click(object sender, EventArgs e)
        {
        }

        private void btnFixedClose_Click(object sender, EventArgs e)
        {
        }

        private void btnNoFixLock_Click(object sender, EventArgs e)
        {
        }

        public void GetPlacedOrderStatus(string orderId)
        {
            var existingInList = allOrders.FirstOrDefault(o => o["OrderId"] == orderId.ToString());
            if (existingInList != null)
            {
                MessageBox.Show($"OrderId : {existingInList["OrderId"]}, Status : {existingInList["Status"]}");
                // Update other fields as needed
            } else
            {
                MessageBox.Show("Invalid requestId!!!");
            }
        }

        private void btnShowFreeze_Click(object sender, EventArgs e)
        {
            if (!clientSocket.IsConnected())
            {
                MessageBox.Show("Not connected to IBKR.");
                return;
            }

            clientSocket.reqAllOpenOrders();               // Request all open orders (manual + API)
            clientSocket.reqAutoOpenOrders(true);          // Bind future manual orders to client 0

            if (!string.IsNullOrEmpty(textTradeBoxExposure.Text))
            {
               GetPlacedOrderStatus(textTradeBoxExposure.Text.ToString());
            }
            else
            {
                MessageBox.Show("Please enter a valid order ID");
            }
        }
        private void btnNoKeepPositions_Click(object sender, EventArgs e)
        {

        }

        private void btnSetBoxRange_Click(object sender, EventArgs e)
        {
            // If you can click this button, you can cancel specific order when you input orderId in txtNeutralPrice.Text
            try
            {
                // Parse the order ID from the TextBox
                if (int.TryParse(txtMaxExposure.Text, out int cancelRequestID))
                {
                    // Send cancellation request for the specified order ID
                    clientSocket.cancelOrder(cancelRequestID, orderCancel);
                    orderchange(cancelRequestID, "Cancelled");

                    textLongTradingPane.AppendText(
                            $"Request ID {cancelRequestID} has been cancelled.\n"
                        );

                    if (!ordersDict.TryGetValue(cancelRequestID, out var orderData))
                    {
                        orderData = new Dictionary<string, string>();
                        ordersDict[cancelRequestID] = orderData;
                    }

                    // Update order data
                    orderData["OrderId"] = cancelRequestID.ToString();
                    orderData["Status"] = "Cancelled";
                    //orderData["Filled"] = filled.ToString();

                    var existingInList = allOrders.FirstOrDefault(o => o["OrderId"] == cancelRequestID.ToString());
                    if (existingInList != null)
                    {
                        existingInList["Status"] = "Cancelled";
                    }
                    else
                    {
                        allOrders.Add(new Dictionary<string, string>(orderData));
                    }
                }
                else
                {
                    MessageBox.Show("Please enter a valid request ID.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error canceling order: {ex.Message}");
            }
        }
        // The function that you are going to cancel specific order 
        private void orderchange(int orderIdchange, String orderstatuschange)
        {
            //if (textShortTradingPane.InvokeRequired)
            //{
            //    textShortTradingPane.Invoke(new Action(() =>
            //    {
            //        textShortTradingPane.Text = $"Order ID: {orderIdchange}, Status: {orderstatuschange}";
            //    }));
            //}
            //else
            //{
            //    textShortTradingPane.Text = $"Order ID: {orderIdchange}, Status: {orderstatuschange}";
            //}
        }
        private void btnLongTakeLoss_Click(object sender, EventArgs e)
        {

        }

        private void btnShortTakeLoss_Click(object sender, EventArgs e)
        {
        }

        private void LoadLongTradingPane()
        {
        }

        private void LoadShortTradingPane()
        {
        }

        private void btnSavePositionFile_Click(object sender, EventArgs e)
        {

        }

        private void btnLoadPositionFile_Click(object sender, EventArgs e)
        {

        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void panel1_Paint(object sender, PaintEventArgs e)
        {

        }

        private void textLongOrderID_TextChanged(object sender, EventArgs e)
        {

        }

        private void txtCurrentLongPositions_TextChanged(object sender, EventArgs e)
        {

        }

        private void textTradeBoxExposure_TextChanged(object sender, EventArgs e)
        {

        }

        private void txtUnrealizedPnL_TextChanged(object sender, EventArgs e)
        {

        }

        private void textCurrentShortUpperOrder_TextChanged(object sender, EventArgs e)
        {

        }

        private void txtShortBoxIndex_TextChanged(object sender, EventArgs e)
        {

        }

        private void labLongBoxUpperBound_Click(object sender, EventArgs e)
        {

        }

        private void textCurrentLongLimitExpo_TextChanged(object sender, EventArgs e)
        {

        }

        private void labLongBoxLimitExpo_Click(object sender, EventArgs e)
        {

        }

        private void checkBoxShowTrades_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void btnTrendBox_Click(object sender, EventArgs e)
        {

        }

        private void btnNoTrendBox_Click(object sender, EventArgs e)
        {

        }

        private void btnKeepPositions_Click(object sender, EventArgs e)
        {

        }

        private void btnPartialTrade_Click(object sender, EventArgs e)
        {

        }

        private void btnNoPartialTrade_Click(object sender, EventArgs e)
        {

        }

        private void btnNeutralBox_Click(object sender, EventArgs e)
        {

        }

        private void btnNoNeutralBox_Click(object sender, EventArgs e)
        {

        }

        private void btnFreezeBox_Click(object sender, EventArgs e)
        {

        }

        private void btnNoFreezeBox_Click(object sender, EventArgs e)
        {

        }

        private void btnBearHedge_Click(object sender, EventArgs e)
        {

        }

        private void btnInactivateRange_Click(object sender, EventArgs e)
        {

        }

        private void btnBullRegion_Click(object sender, EventArgs e)
        {

        }

        private void btnRegionFixed_Click(object sender, EventArgs e)
        {

        }
        //This part is required to use Ewrapper.
        public void error(Exception e) => Console.WriteLine($"Exception: {e.Message}");
        public void error(string str) => Console.WriteLine($"String Error: {str}");
        public void error(int id, int errorCode, string errorMsg) { 
            Console.WriteLine($"ID: {id}, Code: {errorCode}, Msg: {errorMsg}");
            textLongTradingPane.BeginInvoke(new Action(() =>
            {
                textLongTradingPane.AppendText($"Error {errorCode}: {errorMsg}\n");
            }));        
        }
        public void nextValidId(int orderId)
        {
            requestId = orderId;
            Console.WriteLine($"ID: {orderId}");
        }
        public void currentTime(long time) { Console.WriteLine($"ID: {time}"); }
        //public void tickSize(int tickerId, int field, int size) { Console.WriteLine($"ID: {tickerId}"); }
        public void tickOptionComputation(int tickerId, int field, int tickAttrib, double impliedVolatility, double delta, double optPrice, double pvDividend, double gamma, double vega, double theta, double undPrice)
        {
            Console.WriteLine($"Tick Option Computation. ReqId: {tickerId}, Tick Type: {field}, Implied Vol: {impliedVolatility}, Delta: {delta}, Opt Price: {optPrice}, PV Dividend: {pvDividend}, Gamma: {gamma}, Vega: {vega}, Theta: {theta}, Und Price: {undPrice}");
        }
        public void tickGeneric(int tickerId, int tickType, double value) { Console.WriteLine($"ID: {tickerId}"); }
        public void tickString(int tickerId, int tickType, string value) { Console.WriteLine($"ID: {tickerId}"); }

        public void updateAccountTime(string timeStamp)
        {

            Console.WriteLine($"Account time updated: {timeStamp}");
        }
        public void orderStatus(int orderId, string status, decimal filled, decimal remaining,
                                double avgFillPrice, int permId, int parentId,
                                double lastFillPrice, int clientId, string whyHeld,
                                double mktCapPrice)
        {

            if (!ordersDict.TryGetValue(orderId, out var orderData))
            {
                orderData = new Dictionary<string, string>();
                ordersDict[orderId] = orderData;
            }

            // Update order data
            orderData["OrderId"] = orderId.ToString();
            orderData["Status"] = status;
            //orderData["Filled"] = filled.ToString();

            var existingInList = allOrders.FirstOrDefault(o => o["OrderId"] == orderId.ToString());
            if (existingInList != null)
            {
                existingInList["Status"] = status;
            }
            else
            {
                allOrders.Add(new Dictionary<string, string>(orderData));
            }
        }
        public void orderBound(long orderId, int apiClientId, int apiOrderId)
        {
            Console.WriteLine($"Order Bound. OrderId: {orderId}, API ClientId: {apiClientId}, API OrderId: {apiOrderId}");
        }

        public void openOrderEnd()
        {
            Console.WriteLine("All open orders received.\n");
        }

        public void openOrder(int orderId, Contract contract, Order order, OrderState orderState)
        {
           string newOrder = $"OrderId: {orderId}, Action: {order.Action}, OrderType: {order.OrderType}, Status: {orderState.Status}";
            //orderStatuses[orderId] = orderState.Status;
            allOrders.Add(new Dictionary<string, string>
            {
                { "OrderId", orderId.ToString()},
                { "Action", order.Action },
                { "OrderType", order.OrderType },
                { "Status", orderState.Status }
            });
            // Optionally update UI (thread-safe)
            textShortTradingPane.InvokeAsync(() =>
            {
                textShortTradingPane.AppendText($"{newOrder}\n");
            });
        }
        // Helper method to update textShortTradingPane with specific order's status
        private void UpdateOrderStatusDisplay(int orderId)
        {
            //if (textShortTradingPane.InvokeRequired)
            //{
            //    textShortTradingPane.Invoke(new Action(() =>
            //    {
            //        textShortTradingPane.Text = $"Order ID: {orderId}, Status: {status}"; // Display current status
            //    }));
            //}
            //else
            //{
            //    textShortTradingPane.Text = $"Order ID: {orderId}, Status: {status}"; // Display current status
            //}
        }
        private void UpdateAllOrderStatusUI()
        {
            try
            {
                if (textShortTradingPane.InvokeRequired)
                {
                    textShortTradingPane.Invoke(new Action(() =>
                    {
                        if (int.TryParse(textTradeBoxExposure.Text, out int statusRequestID))
                        {
                            UpdateOrderStatusDisplay(statusRequestID);
                        }
                    }));
                }
                else
                {
                    MessageBox.Show("Please input requestId to show stauts");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating UI: {ex.Message}");
            }
        }
        public void wshMetaData(int reqId, string data)
        {
            Console.WriteLine($"WSH MetaData. ReqId: {reqId}, Data: {data}");
        }

        public void wshEventData(int reqId, string data)
        {
            Console.WriteLine($"WSH Event Data. ReqId: {reqId}, Data: {data}");
        }
        public void verifyMessageAPI(string apiData)
        {
            Console.WriteLine($"Verify Message API: {apiData}");
        }

        public void verifyCompleted(bool success, string error)
        {
            Console.WriteLine($"Verify Completed. Success: {success}, Error: {error}");
        }

        public void verifyAndAuthMessageAPI(string apiData, string xyz)
        {
            Console.WriteLine($"Verify and Auth Message API: {apiData}, XYZ: {xyz}");
        }

        public void verifyAndAuthCompleted(bool success, string error)
        {
            Console.WriteLine($"Verify and Auth Completed. Success: {success}, Error: {error}");
        }

        public void userInfo(int userId, string username)
        {
            Console.WriteLine($"User Info. UserId: {userId}, Username: {username}");
        }

        public void updatePortfolio(Contract contract, decimal position, double marketPrice, double marketValue, double averageCost, double unrealizedPNL, double realizedPNL, string accountName)
        {
            Console.WriteLine($"Update Portfolio. Contract: {contract}, Position: {position}, Market Price: {marketPrice}, Market Value: {marketValue}, Average Cost: {averageCost}, Unrealized PNL: {unrealizedPNL}, Realized PNL: {realizedPNL}, Account Name: {accountName}");
        }

        public void updateNewsBulletin(int msgId, int msgType, string bulletin, string url)
        {
            Console.WriteLine($"Update News Bulletin. MsgId: {msgId}, MsgType: {msgType}, Bulletin: {bulletin}, URL: {url}");
        }

        public void updateMktDepthL2(int reqId, int position, string marketMaker, int operation, int side, double price, decimal size, bool isSmartDepth)
        {
            Console.WriteLine($"Update Market Depth L2. ReqId: {reqId}, Position: {position}, Market Maker: {marketMaker}, Operation: {operation}, Side: {side}, Price: {price}, Size: {size}, Is Smart Depth: {isSmartDepth}");
        }

        public void updateMktDepth(int reqId, int position, int operation, int side, double price, decimal size)
        {
            Console.WriteLine($"Update Market Depth. ReqId: {reqId}, Position: {position}, Operation: {operation}, Side: {side}, Price: {price}, Size: {size}");
        }

        public void updateAccountValue(string key, string value, string currency, string accountName)
        {
            Console.WriteLine($"Update Account Value. Key: {key}, Value: {value}, Currency: {currency}, Account Name: {accountName}");
        }

        public void tickSnapshotEnd(int reqId)
        {
            Console.WriteLine($"Tick Snapshot End. ReqId: {reqId}");
        }

        public void tickSize(int reqId, int tickType, decimal size)
        {
            Console.WriteLine($"Tick Size. ReqId: {reqId}, Tick Type: {tickType}, Size: {size}");
        }

        public void tickReqParams(int reqId, double minTick, string bboExchange, int snapshotPermissions)
        {
            Console.WriteLine($"Tick Request Params. ReqId: {reqId}, Min Tick: {minTick}, BBO Exchange: {bboExchange}, Snapshot Permissions: {snapshotPermissions}");
        }

        public void tickNews(int reqId, long timeStamp, string providerCode, string articleId, string headline, string extraData)
        {
            Console.WriteLine($"Tick News. ReqId: {reqId}, TimeStamp: {timeStamp}, Provider Code: {providerCode}, Article ID: {articleId}, Headline: {headline}, Extra Data: {extraData}");
        }

        public void tickEFP(int reqId, int tickType, double basisPoints, string formattedBasisPoints, double totalDividends, int holdDays, string futureExpiry, double dividendImpact, double volatility)
        {
            Console.WriteLine($"Tick EFP. ReqId: {reqId}, Tick Type: {tickType}, Basis Points: {basisPoints}, Formatted Basis Points: {formattedBasisPoints}, Total Dividends: {totalDividends}, Hold Days: {holdDays}, Future Expiry: {futureExpiry}, Dividend Impact: {dividendImpact}, Volatility: {volatility}");
        }
        public void tickByTickMidPoint(int reqId, long time, double midPoint)
        {
            Console.WriteLine($"Tick By Tick MidPoint. ReqId: {reqId}, Time: {time}, MidPoint: {midPoint}");
        }

        public void tickByTickBidAsk(int reqId, long time, double bidPrice, double askPrice, decimal bidSize, decimal askSize, TickAttribBidAsk tickAttrib)
        {
            Console.WriteLine($"Tick By Tick Bid Ask. ReqId: {reqId}, Time: {time}, Bid Price: {bidPrice}, Ask Price: {askPrice}, Bid Size: {bidSize}, Ask Size: {askSize}");
        }

        public void tickByTickAllLast(int reqId, int tickType, long time, double price, decimal size, TickAttribLast tickAttribLast, string exchange, string specialConditions)
        {
            Console.WriteLine($"Tick By Tick All Last. ReqId: {reqId}, Tick Type: {tickType}, Time: {time}, Price: {price}, Size: {size}, Exchange: {exchange}, Special Conditions: {specialConditions}");
        }

        public void symbolSamples(int reqId, ContractDescription[] contractDescriptions)
        {
            Console.WriteLine($"Symbol Samples. ReqId: {reqId}");
            foreach (var contract in contractDescriptions)
            {
                Console.WriteLine($"Contract: {contract}");
            }
        }

        public void softDollarTiers(int reqId, SoftDollarTier[] tiers)
        {
            Console.WriteLine($"Soft Dollar Tiers. ReqId: {reqId}");
            foreach (var tier in tiers)
            {
                Console.WriteLine($"Tier: {tier}");
            }
        }

        public void smartComponents(int reqId, Dictionary<int, KeyValuePair<string, char>> theMap)
        {
            Console.WriteLine($"Smart Components. ReqId: {reqId}");
            foreach (var item in theMap)
            {
                Console.WriteLine($"Key: {item.Key}, Value: {item.Value.Key}, Type: {item.Value.Value}");
            }
        }

        public void securityDefinitionOptionParameterEnd(int reqId)
        {
            Console.WriteLine($"Security Definition Option Parameter End. ReqId: {reqId}");
        }

        public void securityDefinitionOptionParameter(int reqId, string exchange, int underlyingConId, string optionExchange, string optionSymbol, HashSet<string> optionStrike, HashSet<double> optionExpiry)
        {
            Console.WriteLine($"Security Definition Option Parameter. ReqId: {reqId}, Exchange: {exchange}, Underlying ConId: {underlyingConId}, Option Exchange: {optionExchange}, Option Symbol: {optionSymbol}");
        }

        public void scannerParameters(string xml)
        {
            Console.WriteLine($"Scanner Parameters: {xml}");
        }

        public void scannerDataEnd(int reqId)
        {
            Console.WriteLine($"Scanner Data End. ReqId: {reqId}");
        }

        public void scannerData(int reqId, int rank, ContractDetails contractDetails, string distance, string benchmark, string projection, string legsStr)
        {
            Console.WriteLine($"Scanner Data. ReqId: {reqId}, Rank: {rank}, Contract: {contractDetails}, Distance: {distance}, Benchmark: {benchmark}, Projection: {projection}, Legs: {legsStr}");
        }

        public void rerouteMktDepthReq(int reqId, int conId, string exchange)
        {
            Console.WriteLine($"Reroute Market Depth Request. ReqId: {reqId}, ConId: {conId}, Exchange: {exchange}");
        }

        public void rerouteMktDataReq(int reqId, int conId, string exchange)
        {
            Console.WriteLine($"Reroute Market Data Request. ReqId: {reqId}, ConId: {conId}, Exchange: {exchange}");
        }

        public void replaceFAEnd(int reqId, string text)
        {
            Console.WriteLine($"Replace FA End. ReqId: {reqId}, Text: {text}");
        }

        public void receiveFA(int faDataType, string xml)
        {
            Console.WriteLine($"Receive FA. FA Data Type: {faDataType}, XML: {xml}");
        }

        public void realtimeBar(int reqId, long time, double open, double high, double low, double close, decimal volume, decimal wap, int count)
        {
            Console.WriteLine($"Realtime Bar. ReqId: {reqId}, Time: {time}, Open: {open}, High: {high}, Low: {low}, Close: {close}, Volume: {volume}, WAP: {wap}, Count: {count}");
        }

        public void positionMultiEnd(int reqId)
        {
            Console.WriteLine($"Position Multi End. ReqId: {reqId}");
        }

        public void positionMulti(int reqId, string account, string modelCode, Contract contract, decimal pos, double avgCost)
        {
            Console.WriteLine($"Position Multi. ReqId: {reqId}, Account: {account}, Model Code: {modelCode}, Contract: {contract}, Position: {pos}, Avg Cost: {avgCost}");
        }

        public void positionEnd()
        {
            Console.WriteLine("Position End.");
        }

        public void position(string account, Contract contract, decimal pos, double avgCost)
        {
            MessageBox.Show($"Position. Account: {account}, Contract: {contract}, Position: {pos}, Avg Cost: {avgCost}");
        }

        public void pnlSingle(int reqId, decimal pos, double dailyPnL, double unrealizedPnL, double realizedPnL, double value)
        {
            Console.WriteLine($"PNL Single. ReqId: {reqId}, PNL: {pnl}, Daily PnL: {dailyPnL}, Unrealized PNL: {unrealizedPnL}, Realized PNL: {realizedPnL}");
        }

        public void pnl(int reqId, double dailyPnL, double unrealizedPnL, double realizedPnL)
        {
            Console.WriteLine($"PNL. ReqId: {reqId}, Daily PnL: {dailyPnL}, Unrealized PNL: {unrealizedPnL}, Realized PNL: {realizedPnL}");
        }

        
        public void newsProviders(NewsProvider[] newsProviders)
        {
            Console.WriteLine("News Providers:");
            foreach (var provider in newsProviders)
            {
                Console.WriteLine(provider.ToString());
            }
        }

        public void newsArticle(int reqId, int articleType, string article)
        {
            Console.WriteLine($"News Article. ReqId: {reqId}, Article Type: {articleType}, Article: {article}");
        }

        public void mktDepthExchanges(DepthMktDataDescription[] depthMktDataDescriptions)
        {
            Console.WriteLine("Market Depth Exchanges:");
            foreach (var desc in depthMktDataDescriptions)
            {
                Console.WriteLine(desc.ToString());
            }
        }

        public void marketRule(int marketRuleId, PriceIncrement[] priceIncrements)
        {
            Console.WriteLine($"Market Rule. MarketRuleId: {marketRuleId}");
            foreach (var increment in priceIncrements)
            {
                Console.WriteLine(increment.ToString());
            }
        }

        public void marketDataType(int reqId, int marketDataType)
        {
            Console.WriteLine($"Market Data Type. ReqId: {reqId}, Market Data Type: {marketDataType}");
        }

        public void managedAccounts(string accountsList)
        {
            Console.WriteLine($"Managed Accounts: {accountsList}");
        }

        public void historicalTicksLast(int reqId, HistoricalTickLast[] ticks, bool hasMore)
        {
            Console.WriteLine($"Historical Ticks Last. ReqId: {reqId}, Has More: {hasMore}");
            foreach (var tick in ticks)
            {
                Console.WriteLine(tick.ToString());
            }
        }

        public void historicalTicksBidAsk(int reqId, HistoricalTickBidAsk[] ticks, bool hasMore)
        {
            Console.WriteLine($"Historical Ticks Bid Ask. ReqId: {reqId}, Has More: {hasMore}");
            foreach (var tick in ticks)
            {
                Console.WriteLine(tick.ToString());
            }
        }

        public void historicalTicks(int reqId, HistoricalTick[] ticks, bool hasMore)
        {
            Console.WriteLine($"Historical Ticks. ReqId: {reqId}, Has More: {hasMore}");
            foreach (var tick in ticks)
            {
                Console.WriteLine(tick.ToString());
            }
        }

        public void historicalSchedule(int reqId, string startDate, string endDate, string timeZone, HistoricalSession[] sessions)
        {
            Console.WriteLine($"Historical Schedule. ReqId: {reqId}, Start Date: {startDate}, End Date: {endDate}, Time Zone: {timeZone}");
        }

        public void historicalNewsEnd(int reqId, bool hasMore)
        {
            Console.WriteLine($"Historical News End. ReqId: {reqId}, Has More: {hasMore}");
        }

        public void historicalNews(int reqId, string time, string providerCode, string articleId, string headline)
        {
            Console.WriteLine($"Historical News. ReqId: {reqId}, Time: {time}, Provider Code: {providerCode}, Article ID: {articleId}, Headline: {headline}");
        }

        public void historicalDataUpdate(int reqId, Bar bar)
        {
            Console.WriteLine($"Historical Data Update. ReqId: {reqId}, Bar: {bar}");
        }

        public void historicalDataEnd(int reqId, string startDate, string endDate)
        {
            Console.WriteLine($"Historical Data End. ReqId: {reqId}, Start Date: {startDate}, End Date: {endDate}");
        }

        public void historicalData(int reqId, Bar bar)
        {
            MessageBox.Show($"Historical Data. ReqId: {reqId}, Bar: {bar}");
            textLongTradingPane.Invoke((MethodInvoker)(() =>
            {
                textLongTradingPane.Text += $"Historical Data: tickerId={reqId},  price={bar.Close}\n";
            }));
        }

        public void histogramData(int reqId, HistogramEntry[] entries)
        {
            Console.WriteLine($"Histogram Data. ReqId: {reqId}");
            foreach (var entry in entries)
            {
                Console.WriteLine($"Entry: {entry}");
            }
        }

        public void headTimestamp(int reqId, string headTimestamp)
        {
            Console.WriteLine($"Head Timestamp. ReqId: {reqId}, Head Timestamp: {headTimestamp}");
        }

        public void fundamentalData(int reqId, string data)
        {
            Console.WriteLine($"Fundamental Data. ReqId: {reqId}, Data: {data}");
        }

        public void familyCodes(FamilyCode[] familyCodes)
        {
            Console.WriteLine("Family Codes:");
            foreach (var code in familyCodes)
            {
                Console.WriteLine($"Code: {code}");
            }
        }

        public void execDetailsEnd(int reqId)
        {
            Console.WriteLine($"Exec Details End. ReqId: {reqId}");
        }

        public void execDetails(int reqId, Contract contract, Execution execution)
        {
            Console.WriteLine($"Exec Details. ReqId: {reqId}, Contract: {contract}, Execution: {execution}");
        }

        public void error(int id, int errorCode, string errorMsg, string errorString)
        {
            Console.WriteLine($"Error. ID: {id}, Code: {errorCode}, Msg: {errorMsg}, String: {errorString}");
        }

        public void displayGroupUpdated(int reqId, string contractInfo)
        {
            Console.WriteLine($"Display Group Updated. ReqId: {reqId}, Contract Info: {contractInfo}");
        }

        public void displayGroupList(int reqId, string groups)
        {
            Console.WriteLine($"Display Group List. ReqId: {reqId}, Groups: {groups}");
        }

        public void deltaNeutralValidation(int reqId, DeltaNeutralContract deltaNeutralContract)
        {
            Console.WriteLine($"Delta Neutral Validation. ReqId: {reqId}, Delta Neutral Contract: {deltaNeutralContract}");
        }

        public void contractDetailsEnd(int reqId)
        {
            Console.WriteLine($"Contract Details End. ReqId: {reqId}");
        }

        public void contractDetails(int reqId, ContractDetails contractDetails)
        {
            double minTickSize = contractDetails.MinTick; // Minimum tick size

            // Determine the pip value based on the contract type
            if (contractDetails.Contract.SecType == "CASH") // Forex
            {
                pipValue = 2 * minTickSize;
            }
            if (contractDetails.Contract.SecType == "CRYPTO") // Forex
            {
                pipValue = 0.2 * minTickSize;
            }
            else if (contractDetails.Contract.SecType == "STK" || contractDetails.Contract.SecType == "FUT")
            {
                // For stocks and futures, we can use minTickSize directly as it represents the smallest price movement.
                pipValue = minTickSize;
            }

            Console.WriteLine($"Contract Details. ReqId: {reqId}, Contract Details: {contractDetails}");


        }


        public void connectionClosed()
        {
            Console.WriteLine("Connection Closed.");
        }

        public void connectAck()
        {
            Console.WriteLine("Connect Acknowledged.");
        }

        public void completedOrdersEnd()
        {
            Console.WriteLine("Completed Orders End.");
        }

        public void completedOrder(Contract contract, Order order, OrderState orderState)
        {
            Console.WriteLine($"Completed Order. Contract: {contract}, Order: {order}, OrderState: {orderState}");
        }

        public void commissionReport(CommissionReport commissionReport)
        {
            Console.WriteLine($"Commission Report: {commissionReport}");
        }

        public void bondContractDetails(int reqId, ContractDetails contractDetails)
        {
            Console.WriteLine($"Bond Contract Details. ReqId: {reqId}, Contract Details: {contractDetails}");
        }

        public void accountUpdateMultiEnd(int reqId)
        {
            Console.WriteLine($"Account Update Multi End. ReqId: {reqId}");
        }

        public void accountUpdateMulti(int reqId, string account, string modelCode, string key, string value, string currency)
        {
            Console.WriteLine($"Account Update Multi. ReqId: {reqId}, Account: {account}, Model Code: {modelCode}, Key: {key}, Value: {value}, Currency: {currency}");
        }

        public void accountSummaryEnd(int reqId)
        {
            Console.WriteLine($"Account Summary End. ReqId: {reqId}");
        }

        public void accountSummary(int reqId, string account, string tag, string value, string currency)
        {
            Console.WriteLine($"Account Summary. ReqId: {reqId}, Account: {account}, Tag: {tag}, Value: {value}, Currency: {currency}");
        }
        public void accountDownloadEnd(string account)
        {
            Console.WriteLine($"Account Download End. Account: {account}");
        }

        private void TextLongTradePrice_TextChanged(object sender, EventArgs e)
        {

        }

        private void TextShortTradePrice_TextChanged(object sender, EventArgs e)
        {

        }

        private void textCurrentShortUpperPosition_TextChanged(object sender, EventArgs e)
        {

        }

        private void textLongTradingPane_TextChanged(object sender, EventArgs e)
        {

        }

        private void txtNeutralPrice_TextChanged(object sender, EventArgs e)
        {

        }

        private void textShortTradingPane_TextChanged(object sender, EventArgs e)
        {

        }

        private void button1_Click_2(object sender, EventArgs e)
        {
            
        }
    }
}
