# CWS Assistant (Colorful-palette Workspace Solution)
**🌐 [官方網站 | Official Website](https://cleanws.us.ci/)**

[![GitHub Release](https://img.shields.io/github/v/release/Colorful-Palette/CWS?style=flat-square)](https://github.com/Colorful-Palette/CWS/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](https://opensource.org/licenses/MIT)
[![Build Status](https://img.shields.io/github/actions/workflow/status/Colorful-Palette/CWS/release.yml?style=flat-square)](https://github.com/Colorful-Palette/CWS/actions)

## 📖 專案簡介 | Introduction

**CWS Assistant** 是一個基於 .NET 8 WPF 開發的輕量化工具，旨在解決 **Microsoft Office** 與 **WPS Office** 同時安裝時常見的檔案關聯衝突與服務搶佔問題。

**CWS Assistant** is a lightweight utility built with .NET 8 WPF. It is designed to resolve common file association conflicts and service preemption issues that occur when **Microsoft Office** and **WPS Office** are installed on the same system.

---

## ✨ 核心功能 | Key Features

### 1. 檔案關聯快速切換 | File Association Toggle
* 一鍵修復 PPT、PPTX 檔案關聯，在 Office 與 WPS 之間無縫切換，並自動重新整理系統圖示快取。
* Quickly fix PPT/PPTX file associations, toggle seamlessly between Office and WPS, and automatically refresh system icon caches.

### 2. 懸浮球互動模式 | Floating Ball Interface
* 簡約而不簡單的懸浮球介面，讓你無需打開主視窗即可快速執行修復操作。
* A clean and functional floating ball interface, allowing you to perform quick repairs without opening the main window.

### 3. 單實例與智慧喚醒 | Single Instance & Auto-Wake
* 採用 Mutex 技術防止程式多開。重複啟動時會自動喚醒已存在的視窗，確保系統資源不被浪費。
* Utilizes Mutex technology to prevent multiple instances. Launching the app again will automatically wake the existing window, ensuring optimal resource usage.

### 4. 啟動偏好設定 | Startup Preferences
* 支援自定義啟動行為，可選擇靜默啟動至懸浮球或直接顯示主介面。
* Supports custom startup behavior—choose between starting silently as a floating ball or opening the main UI.

---

## 🛠️ 技術棧 | Technology Stack

* **Language:** C# 12
* **Framework:** .NET 8.0 (WPF)
* **API:** Win32 API (User32.dll) for window management
* **Installer:** Inno Setup
* **Automation:** GitHub Actions (CI/CD)

---

## 🚀 快速開始 | Quick Start

### 1. 安裝與執行 | Installation & Launch
* 從 [Releases](https://github.com/Mrmiaomrzh/CWS/releases) 頁面下載最新版本的安裝程式 (`.exe`)。安裝完成後，程式會自動運行並顯示在桌面右下角或懸浮球。
* Download the latest installer (`.exe`) from the [Releases](https://github.com/Mrmiaomrzh/CWS/releases) page. After installation, CWS will launch and appear in the tray or as a floating ball.

### 2. 常用操作 | Common Tasks
* **切換關聯 (Toggle Association):** * 在主介面的「文件關聯」分頁，點擊 **[設為 Office 打開]** 或 **[設為 WPS 打開]** 即可即時修復關聯。
  * Navigate to the "File Association" tab, then click **[Set to Office]** or **[Set to WPS]** to repair associations instantly.
* **快速修復 (Quick Fix):** * 透過懸浮球右鍵選單，無需打開主介面即可快速執行圖示刷新或 PPT 修復。
  * Right-click the floating ball for quick access to icon refreshing or PPT repairs without opening the main window.

---

### 💡 小貼士 | Pro Tip
* **繁中：** 如果你發現圖示沒有變化，請點擊「清除圖標緩存」，CWS 會自動重啟資源管理器以套用更改。
* **EN:** If icons don't update, click "Clear Icon Cache." CWS will restart Windows Explorer automatically to apply changes.

---

### 💖 特別鳴謝 | Special Thanks
* **翻譯支援 (Translation):**
  * 特別鳴謝 **[YL1647Rui](https://github.com/YL1647Rui)** 提供地道翻譯支援。
  * Special thanks to **[YL1647Rui](https://github.com/YL1647Rui)** for providing professional localized translation support.

---

## 📄 開源協議 | License

本項目採用 **MIT License**。
This project is licensed under the **MIT License**.

> *BanG Dream! It's MyGO!!!!!*
> —— 「要组一辈子Colorful-Palette。」
