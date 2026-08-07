# 💻 PC Health Dashboard

**PC Health Dashboard** is a modern, high-performance system monitoring application built with **.NET 8** and **WPF**. Designed with a premium dark-themed UI and fluid animations, it provides real-time insights into your computer's vital hardware statistics.

Whether you are gaming, rendering, or simply keeping an eye on your system, PC Health Dashboard delivers precise, synchronized hardware data in a stunning visual interface.

---

## ✨ Key Features

- **📊 Comprehensive Monitoring:** Tracks real-time temperature, load, and usage for CPU, GPU, RAM, and Storage using the powerful `LibreHardwareMonitor` engine.
- **🌐 Network Analytics:** Accurately measures live upload/download speeds (Mbps), latency (ping), and packet loss.
- **💯 Intelligent Health Score:** Automatically calculates an overall "Health Score" for your PC based on temperatures, free space, and network stability.
- **🎨 Premium UI/UX:** Features a sleek, modern dark mode with glassmorphism elements, dynamic gradients, and smooth sparkline charts built with `LiveCharts2`.
- **📌 KittyWindow (Floating Widget):** Includes a compact, always-on-top, translucent widget. The widget syncs flawlessly with the main dashboard, allowing you to monitor your system seamlessly while inside other full-screen apps or games.
- **⚡ Zero Lag & Low Overhead:** Highly optimized asynchronous data polling ensures smooth updates without bogging down your machine.

---

## 📸 Screenshots

*(Replace the image links below with actual screenshots of your app)*

**Main Dashboard**
![Main Dashboard](https://via.placeholder.com/1000x600.png?text=Main+Dashboard+Screenshot+Here)

**KittyWindow (Always-on-top Widget)**
![KittyWindow Widget](https://via.placeholder.com/350x450.png?text=KittyWindow+Screenshot+Here)

---

## 🛠️ Technology Stack

- **Framework:** .NET 8.0 (WPF)
- **Architecture:** MVVM (Model-View-ViewModel) via `CommunityToolkit.Mvvm`
- **Hardware Tracking:** `LibreHardwareMonitorLib`
- **Charting:** `LiveChartsCore.SkiaSharpView.WPF`

---

## 🚀 Getting Started

### Prerequisites
- Windows 10 or Windows 11
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- The app must be run as **Administrator** so that `LibreHardwareMonitor` can access low-level CPU/GPU hardware sensors.

### Build from source
1. Clone the repository:
   ```bash
   git clone https://github.com/your-username/PC_Health_Dashboard.git
   ```
2. Open the solution in **Visual Studio 2022** or **JetBrains Rider**.
3. Restore NuGet packages and Build the project.
4. Run the application (Ensure you start Visual Studio as Administrator for hardware sensors to work).

---

## ⌨️ Shortcuts

- **Minimize to Tray / Hide:** Click the minimize button on the main dashboard.
- **Toggle KittyWindow:** Press `Ctrl + Shift + Space` globally to show/hide the floating widget.

---

## 🤝 Contributing

Contributions, issues, and feature requests are welcome! Feel free to check the [issues page](https://github.com/your-username/PC_Health_Dashboard/issues).

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
