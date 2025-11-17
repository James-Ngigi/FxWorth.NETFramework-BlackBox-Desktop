# FxWorth Desktop Application - Automated Binary Options Trading

## Project Overview

FxWorth is a high-performance desktop application built with C# and Windows Forms, designed for automated trading of binary options on the Deriv platform.  It leverages the Deriv API via WebSockets for real-time market data and trade execution.  The application features a sophisticated risk management system, including a novel "Hierarchy System" for controlled loss recovery, and supports multiple trading accounts.  FxWorth is designed for experienced traders who understand the risks associated with binary options trading and are comfortable with automated trading strategies.

## Current Status

*   **Core Functionality:**
    *   **Automated Trading:**  Executes pair trades (simultaneous "Higher" and "Lower" contracts) (Straddle technique) based on user-defined parameters and the ATR Dual ROC indicator.
    *   **Real-time Data:**  Connects to the Deriv API via WebSockets to receive real-time market data (ticks and candles).
    *   **Multiple Account Management:**  Supports managing multiple Deriv accounts simultaneously, each with independent trading parameters.
    *   **Configurable Trading Parameters:**  Allows users to configure various trading parameters, including:
        *   Stake amount
        *   Martingale level
        *   Take Profit
        *   Max Drawdown (triggers the Hierarchy System)
        *   Barrier Offset
        *   Duration and Duration Unit
        *   ATR Dual ROC parameters (period, compression/expansion thresholds, lookback periods)
    *   **Hierarchy Recovery System:**  Implements a sophisticated, multi-layered recovery system to mitigate losses.  This system divides large losses into smaller, manageable recovery targets across a hierarchy of levels and layers.  Each level can have custom parameters (Martingale level, Max Drawdown, Barrier Offset, Initial Stake).
    *   **Custom Layer Configuration:**  Provides a user interface for configuring custom parameters for individual layers within the hierarchy.
    *   **Logging:**  Uses NLog for detailed logging of trading activity, errors, and system events.
    *   **API Compliance:**  Includes mechanisms to manage log files and ensure compliance with Deriv API usage terms.
    *   **Power Management:**  Prevents the system from sleeping during active trading sessions.
    *   **UI Responsiveness:**  Employs techniques like double buffering and optimized drawing to ensure a smooth and responsive user interface.

*   **Technology Stack:**
    *   C#: Primary programming language.
    *   Windows Forms:  GUI framework.
    *   .NET Framework 4.8.1
    *   Deriv API (via WebSockets):  Real-time market data and trade execution.
    *   NLog: Logging framework.
    *   Newtonsoft.Json: JSON serialization/deserialization.
    *   TA-Lib: Technical analysis library (for ATR calculation).
    *   SuperSocket.ClientEngine, WebSocket4Net: WebSocket communication.

## Key Features

*   **Hierarchy Recovery System:**  This is the core differentiating feature of FxWorth.  It provides a structured and controlled approach to loss recovery, significantly reducing the risk associated with traditional Martingale strategies.
    *   **Layered Structure:**  Divides large losses into smaller, manageable recovery targets across multiple layers and levels.
    *   **Customizable Parameters:**  Allows users to configure parameters for each layer and level, including initial stake, Martingale level, maximum drawdown, and barrier offset.
    *   **Seamless Integration:**  Integrates seamlessly with the existing Martingale recovery logic.
    *   **Granular Risk Control:**  Provides fine-grained control over risk exposure during recovery.

*   **Pair Trading:**  Employs the straddle pair trading strategy to hedge trades and reduce risk.

*   **ATR Dual ROC "Sniper Model" Signals:**  Uses the Average True Range with dual Rate of Change calculations to generate trading signals based on volatility compression followed by expansion patterns.

*   **Configurable Trading Parameters:**  Offers extensive customization options for trading parameters, allowing users to tailor the trading strategy to their risk tolerance and market conditions.

*   **Multiple Account Management:**  Supports managing and trading on multiple Deriv accounts simultaneously.

*   **Real-time Data and Execution:**  Uses WebSockets for low-latency communication with the Deriv API, ensuring real-time data updates and fast trade execution.

*   **Robust Error Handling:**  Includes comprehensive error handling and logging to ensure stability and provide insights into system behavior.

* **User-Friendly Interface:** A well-designed, responsive, and DPI-aware Windows Forms UI.

## Current Data Flow

*	FxWorthMainUI loads credentials into TokenStorage.
*	TokenStorage creates AuthClient instances for each credential.
*	User clicks "Start" in FxWorthMainUI.
*	TokenStorage.StartAll() starts the AuthClient instances, connecting them directly to Deriv.
*	TokenStorage subscribes to market data via MarketDataClient.
*	MarketDataClient feeds data to the ATR_Dual_ROC indicator.
*	ATR_Dual_ROC.Crossover event triggers TokenStorage.OnCrossover.
*	TokenStorage.OnCrossover iterates through active AuthClient instances and calls their Buy/Sell methods if conditions are met.
*	AuthClient sends trade requests directly to Deriv.
*	AuthClient receives transaction/balance updates directly from Deriv and updates its internal Balance and Pnl properties.
*	FxWorthMainUI periodically polls TokenStorage (via ClientsStateChanged event triggered by a timer) to update the DataGridViews with Balance and Pnl from each AuthClient.

## Future Development

*   **Integration with FxWorth Web Application:**  The primary focus of future development is to integrate the FxWorth Desktop application with the FxWorth Web application.  This will enable:
    *   **Centralized Account Management:**  Users will manage their Deriv API tokens and trading parameters through the web application.
    *   **Remote Configuration:**  Users will be able to configure and control the desktop trading bot remotely via the web interface.
    *   **Data Synchronization:**  Trading data (profit/loss, trade history) will be synchronized between the desktop application and the web application's database.
    *   **Enhanced Reporting and Analytics:**  The web application will provide more comprehensive reporting and analytics capabilities.

*   **Improved UI/UX:**  Continued to refinement to the user interface and user experience based on user feedback.

## Getting Started

1.  **Prerequisites:**
    *   .NET Framework 4.8.1
    *   A Deriv API token (obtained from the Deriv website)
    *   Visual Studio (for development and building)

2.  **Installation:**
    *   Clone the repository: `git clone [repository URL]`
    *   Open the solution (`FxWorth.sln`) in Visual Studio.
    *   Restore NuGet packages.
    *   Build the solution.

3.  **Configuration:**
    *   Add your Deriv API token(s) and App ID(s) to the `tokens.json` file.
    *   Configure your desired trading parameters in the UI or through the `layout.json` file.

4.  **Running the Application:**
    *   Run the `FxWorth.exe` executable.

## Testing ROI Calibration & Polling

1. **Configure a target ROI:** Repurpose the barrier offset fields in the UI to capture the desired return percentage (e.g., 250%). Ensure `DesiredReturnPercent` and the base stake are both greater than zero.
2. **Start a trading session:** When `TokenStorage` invokes `EnsureReturnTargetBarrier`, the adaptive search will begin. Watch the log for entries such as `Calibration sample ... ROI ...` followed by `Calibrated barrier ...`.
3. **Validate proposal feedback:** Each proposal response now logs the barrier, ask, payout, and computed ROI. Confirm that the ROI stabilizes within the ±2% tolerance window around the target.
4. **Observe polling refreshes:** After the initial calibration succeeds, a background poller re-validates the barrier roughly every five seconds. Successful refreshes appear as `Polling recalibrated barrier ...` messages; skipped iterations occur when the last calibration is still fresh.
5. **Stress-test different markets:** Switch symbols/durations to ensure the decimal precision fallback (0.001 → 0.01) kicks in only when the broker rejects fine offsets. The log will note `Broker rejected barrier precision` when the fallback engages.
6. **Failure handling:** If the adaptive search exhausts its bounds without finding a match, the log will emit `Adaptive barrier search hit bounds`. Adjust `BarrierSearchMin/Max` or the desired ROI and retry.

## Disclaimer

Binary options trading involves significant risk.  FxWorth is a tool to assist with trading, but it does not guarantee profits.  Users are responsible for understanding the risks involved and for managing their own trading strategies.  Use this software responsibly and at your own risk.

## Contributing

Contributions to FxWorth are welcome!  Please see the contributing guidelines for more information.

## License

This project is currently closed-source and proprietary.