# 💻 PC Health Dashboard

**PC Health Dashboard** is a modern, high-performance system monitoring application built with **.NET 10** and **WPF**. Designed with a premium dark-themed UI and fluid animations, it provides real-time insights into your computer's vital hardware statistics.

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

**Main Dashboard**
<img width="1602" height="996" alt="{20A47FAC-3CE8-4330-B679-BFC262338008}" src="https://github.com/user-attachments/assets/382e87e1-d38b-4a4e-b539-52fd19e65415" />

**KittyWindow (Always-on-top Widget)**
<img width="399" height="514" alt="{29C908C7-33D6-406D-8C05-75DBCEC9E99E}" src="https://github.com/user-attachments/assets/f4608d7d-ddaf-467d-9711-1f2a0c1e371c" />


---

## 🛠️ Technology Stack

- **Framework:** .NET 10.0 (WPF)
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

### Publish ultra-lightweight version (< 20MB)
To publish the app as a tiny executable (framework-dependent), run:
```bash
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

---

## ⌨️ Shortcuts

- **Minimize to Tray / Hide:** Click the minimize button on the main dashboard.
- **Toggle KittyWindow:** Press `Ctrl + Shift + Space` globally to show/hide the floating widget.

---

## 🤝 Contributing

Contributions, issues, and feature requests are welcome! Feel free to check the [issues page](https://github.com/your-username/PC_Health_Dashboard/issues).

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

