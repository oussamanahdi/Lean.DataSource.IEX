/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using QuantConnect.Data;
using QuantConnect.Logging;
using QuantConnect.Securities;
using QuantConnect.Data.Market;

namespace QuantConnect.IEX
{
    public class IEXDataDownloader : IDataDownloader, IDisposable
    {
        private readonly IEXDataQueueHandler _handler;

        private readonly MarketHoursDatabase _marketHoursDatabase;

        public IEXDataDownloader()
        {
            _handler = new IEXDataQueueHandler();
            _marketHoursDatabase = MarketHoursDatabase.FromDataFolder();
        }

        public void Dispose()
        {
            _handler.Dispose();
        }

        /// <summary>
        /// Get historical data enumerable for a single symbol, type and resolution given this start and end time (in UTC).
        /// </summary>
        /// <param name="dataDownloaderGetParameters">model class for passing in parameters for historical data</param>
        /// <returns>Enumerable of base data for this symbol</returns>
        public IEnumerable<BaseData> Get(DataDownloaderGetParameters dataDownloaderGetParameters)
        {
            var symbol = dataDownloaderGetParameters.Symbol;
            var resolution = dataDownloaderGetParameters.Resolution;
            var startUtc = dataDownloaderGetParameters.StartUtc;
            var endUtc = dataDownloaderGetParameters.EndUtc;
            var tickType = dataDownloaderGetParameters.TickType;

            if (tickType != TickType.Trade)
            {
                yield break;
            }

            if (resolution != Resolution.Daily && resolution != Resolution.Minute) 
            {
                Log.Error($"{nameof(IEXDataDownloader)}.{nameof(Get)}: InvalidResolution. {resolution} resolution not supported, no history returned");
                yield break;
            }

            if (endUtc < startUtc)
            {
                Log.Error($"{nameof(IEXDataDownloader)}.{nameof(Get)}:InvalidDateRange. The history request start date must precede the end date, no history returned");
                yield break;
            }

            var exchangeHours = _marketHoursDatabase.GetExchangeHours(symbol.ID.Market, symbol, symbol.SecurityType);
            var dataTimeZone = _marketHoursDatabase.GetDataTimeZone(symbol.ID.Market, symbol, symbol.SecurityType);

            var historyRequests = new[] {
                new HistoryRequest(startUtc,
                                   endUtc,
                                   typeof(TradeBar),
                                   symbol,
                                   resolution, 
                                   exchangeHours,
                                   dataTimeZone,
                                   resolution,
                                   true,
                                   false,
                                   DataNormalizationMode.Raw,
                                   TickType.Trade)
            };

            foreach (var slice in _handler.GetHistory(historyRequests, TimeZones.EasternStandard))
            {
                yield return slice[symbol];
            }
        }
    }
}
