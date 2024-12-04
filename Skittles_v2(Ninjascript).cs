#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
	public class Skittles_v2 : Strategy
	{	
		private int lastHH = 0;
		private int lastLL = 0;
		
		private bool isLong = false;
		private bool isShort = false;
		
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"written by Jake";
				Name										= "Skittles_v2";
				Calculate									= Calculate.OnEachTick;
				EntriesPerDirection							= 1;
				EntryHandling								= EntryHandling.AllEntries;
				IsExitOnSessionCloseStrategy				= true;
				ExitOnSessionCloseSeconds					= 30;
				IsFillLimitOnTouch							= false;
				MaximumBarsLookBack							= MaximumBarsLookBack.TwoHundredFiftySix;
				OrderFillResolution							= OrderFillResolution.Standard;
				Slippage									= 0;
				StartBehavior								= StartBehavior.WaitUntilFlat;
				TimeInForce									= TimeInForce.Gtc;
				TraceOrders									= false;
				RealtimeErrorHandling						= RealtimeErrorHandling.StopCancelClose;
				StopTargetHandling							= StopTargetHandling.PerEntryExecution;
				BarsRequiredToTrade							= 20;
				// Disable this property for performance gains in Strategy Analyzer optimizations
				// See the Help Guide for additional information
				IsInstantiatedOnEachOptimizationIteration	= true;
				
				period				= 20;
				coeff				= 1;
				AP					= 5;
				startTime			= DateTime.Parse("09:30", System.Globalization.CultureInfo.InvariantCulture);
				endTime				= DateTime.Parse("15:00", System.Globalization.CultureInfo.InvariantCulture);
				tp					= 30;
				sl					= 30;
				contractSize 		= 1;
			}
			else if (State == State.Configure)
			{				
				AddPlot(Brushes.Transparent, "MagicTrend");
				SetProfitTarget(CalculationMode.Ticks, tp);
				SetStopLoss(CalculationMode.Ticks, sl);
			}
		}

		protected override void OnBarUpdate()
		{
			if (CurrentBar < Math.Max(period, AP))
			{
				MagicTrend[0] = 0;
				return;
			}
			
			DateTime sessionStartTime = new DateTime(Time[0].Year, Time[0].Month, Time[0].Day, startTime.Hour, startTime.Minute, 0);
			DateTime sessionEndTime = new DateTime(Time[0].Year, Time[0].Month, Time[0].Day, endTime.Hour, endTime.Minute, 0);
			
			// Trend Magic Indicator
			
			bool isHigherHigh = High[0] > High[1] && High[1] > High[2] && Close[0] > Close[1] && Close[1] > Close[2] && Close[0] > Open[0] && Close[1] > Open[1] && Close[2] > Open[2] && Low[0] > Open[1] && Low[1] > Open[2] && (Open[0] + Low[0]) < (Open[0] + Close[0]);
			bool isLowerLow = Low[0] < Low[1] && Low[1] < Low[2] && Close[0] < Close[1] && Close[1] < Close[2] && Close[0] < Open[0] && Close[1] < Open[1] && Close[2] < Open[2] && High[0] < Open[1] && High[1] < Open[2];
			
			if (isHigherHigh)
			{
				BarBrushes[0] = Brushes.Black;
				BarBrushes[1] = Brushes.Black;
				if (Close[0] > Open[0])
					BarBrushes[2] = Brushes.Black;
			}
			
			if (isLowerLow)
			{
				BarBrushes[0] = Brushes.Yellow;
				BarBrushes[1] = Brushes.Yellow;
				if (Close[0] < Open[0])
					BarBrushes[2] = Brushes.Yellow;
			}
			
			// Supply and Demand Zone Indicator
			
			double atr = SMA(ATR(1), AP)[0];
			double upT = Low[0] - atr * coeff;
			double downT = High[0] + atr * coeff;
			
			MagicTrend[0] = CCI(period)[0] >= 0 ? (upT < MagicTrend[1] ? MagicTrend[1] : upT) : (downT > MagicTrend[1] ? MagicTrend[1] : downT);
			PlotBrushes[0][0] = CCI(period)[0] >= 0 ? Brushes.Blue : Brushes.Red;
			
			if (CCI(period)[0] >= 0 && CCI(period)[1] < 0)
			{
				if (Position.MarketPosition == MarketPosition.Short)				ExitShort();
				isLong = true;
				isShort = false;
			}			
			else if (CCI(period)[0] < 0 && CCI(period)[1] >= 0)
			{
				if (Position.MarketPosition == MarketPosition.Long)					ExitLong();
				isLong = false;
				isShort = true;
			}
						
			bool isSession = false;
						
			if (ToTime(Time[0]) >= ToTime(sessionStartTime) && ToTime(Time[0]) <= ToTime(sessionEndTime) && ToTime(sessionStartTime) < ToTime(sessionEndTime))
				isSession = true;
			else if ((ToTime(Time[0]) >= ToTime(sessionStartTime) || ToTime(Time[0]) <= ToTime(sessionEndTime)) && ToTime(sessionStartTime) > ToTime(sessionEndTime))
				isSession = true;

			if (!isSession)
			{
				if (Position.MarketPosition == MarketPosition.Long)					ExitLong();
				if (Position.MarketPosition == MarketPosition.Short)				ExitShort();
				return;
			}
			
			BackBrush = Brushes.White;
			Brush newBrush = BackBrush.Clone();
			newBrush.Opacity = 0.25;
			newBrush.Freeze();
			BackBrush = newBrush;
			
			if (CCI(period)[0] >= 0 && isHigherHigh && CurrentBar - lastHH > 1 && isLong)
			{
				EnterLong(0, contractSize, "Long" + CurrentBar);
				lastHH = CurrentBar;
				isLong = false;
			}
			
			if (CCI(period)[0] < 0 && isLowerLow && CurrentBar - lastLL > 1 && isShort)
			{
				EnterShort(0, contractSize, "Short" + CurrentBar);
				lastLL = CurrentBar;
				isShort = false;
			}
			
		}

		#region Properties
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="CCI period", Order=1, GroupName="1.S&D zone")]
		public int period
		{ get; set; }

		[NinjaScriptProperty]
		[Range(0, double.MaxValue)]
		[Display(Name="ATR Multiplier", Order=2, GroupName="1.S&D zone")]
		public double coeff
		{ get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="ATR Period", Order=3, GroupName="1.S&D zone")]
		public int AP
		{ get; set; }

		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name="Start Time", Order=4, GroupName="2.Strategy")]
		public DateTime startTime
		{ get; set; }

		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name="End Time", Order=5, GroupName="2.Strategy")]
		public DateTime endTime
		{ get; set; }

		[NinjaScriptProperty]
		[Range(1, double.MaxValue)]
		[Display(Name="Take Profit", Order=6, GroupName="2.Strategy")]
		public double tp
		{ get; set; }

		[NinjaScriptProperty]
		[Range(1, double.MaxValue)]
		[Display(Name="Stop Loss", Order=7, GroupName="2.Strategy")]
		public double sl
		{ get; set; }
		
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="Lot Size", Order=8, GroupName="2.Strategy")]
		public int contractSize
		{ get; set; }
		
		[Browsable(false)]
		[XmlIgnore]
		public Series<double> MagicTrend
		{
			get { return Values[0]; }
		}
		#endregion

	}
}
