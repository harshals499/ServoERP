using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace HVAC_Pro_Desktop.Services
{
    public static class LanguageManager
    {
        public const string English = "English";
        public const string Marathi = "मराठी";
        public const string Hindi = "हिन्दी";

        private static readonly Dictionary<string, Dictionary<string, string>> _strings;
        private static readonly Dictionary<object, string> _originalText = new Dictionary<object, string>();
        private static readonly object _sync = new object();

        public static event EventHandler LanguageChanged;

        public static string CurrentLanguage { get; private set; } = English;

        static LanguageManager()
        {
            _strings = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                [English] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Dashboard"] = "Dashboard",
                    ["Clients"] = "Clients",
                    ["Contracts"] = "Contracts",
                    ["Invoices"] = "Invoices",
                    ["Payments"] = "Payments",
                    ["SLA Dashboard"] = "SLA Dashboard",
                    ["Quotations"] = "Quotations",
                    ["Reports"] = "Reports",
                    ["Settings"] = "Settings",
                    ["Vendors"] = "Vendors",
                    ["Purchases"] = "Purchases",
                    ["Inventory"] = "Inventory",
                    ["Employees"] = "Employees",
                    ["Payroll"] = "Payroll",
                    ["Dispatch Center"] = "Dispatch Center",
                    ["Jobs"] = "Jobs",
                    ["Retired"] = "Retired",
                    ["Master Data"] = "Master Data",
                    ["WhatsApp Hub"] = "WhatsApp Hub",
                    ["Tally"] = "Tally",
                    ["Settings & Support"] = "SETTINGS & SUPPORT",
                    ["Data & Compliance"] = "DATA & COMPLIANCE",
                    ["HR & Payroll"] = "HR & PAYROLL",
                    ["Operations"] = "OPERATIONS",
                    ["Sales"] = "SALES",
                    ["Save"] = "Save",
                    ["Cancel"] = "Cancel",
                    ["Edit"] = "Edit",
                    ["Delete"] = "Delete",
                    ["Close"] = "Close",
                    ["Add New"] = "Add New",
                    ["Language"] = "Language",
                    ["Search"] = "Search",
                    ["Alerts"] = "Alerts",
                    ["Logout"] = "Logout",
                    ["Change Password"] = "Change Password",
                    ["Help & Support"] = "Help & Support",
                    ["AI Copilot"] = "AI Copilot",
                    ["Quick Action"] = "Quick Action",
                    ["Renew license"] = "Renew license",
                    ["Install update"] = "Install update",
                    ["Remind me later"] = "Remind me later",
                    ["SearchPlaceholder"] = "Search apps, clients, jobs, invoices, purchases...",
                    ["Customize"] = "Customize",
                    ["Good"] = "Good",
                    ["morning"] = "morning",
                    ["afternoon"] = "afternoon",
                    ["evening"] = "evening",
                    ["BusinessToday"] = "Here's what's happening across your business today.",
                    ["This Month"] = "This Month",
                    ["Last Month"] = "Last Month",
                    ["This Quarter"] = "This Quarter",
                    ["This Year"] = "This Year",
                    ["Sales / Quotations"] = "Sales / Quotations",
                    ["Open Quotations"] = "Open Quotations",
                    ["Value (MTD)"] = "Value (MTD)",
                    ["Jobs / Projects"] = "Jobs / Projects",
                    ["Total Active Jobs"] = "Total Active Jobs",
                    ["In Progress"] = "In Progress",
                    ["Overdue"] = "Overdue",
                    ["Purchase Orders"] = "Purchase Orders",
                    ["Open POs"] = "Open POs",
                    ["Overdue Invoices"] = "Overdue Invoices",
                    ["Overdue Amount"] = "Overdue Amount",
                    ["Pending Payables"] = "Pending Payables",
                    ["Net Cash Flow"] = "Net Cash Flow",
                    ["Active Vendors"] = "Active Vendors",
                    ["Overdue Payables"] = "Overdue Payables",
                    ["Active Clients"] = "Active Clients",
                    ["Outstanding"] = "Outstanding",
                    ["Materials / Procurement"] = "Materials / Procurement",
                    ["To Order Items"] = "To Order Items",
                    ["Priced Items"] = "Priced Items",
                    ["Active Employees"] = "Active Employees",
                    ["On Leave Today"] = "On Leave Today",
                    ["Service Operations"] = "Service Operations",
                    ["Open Service Tickets"] = "Open Service Tickets",
                    ["High Priority"] = "High Priority",
                    ["Financial Overview (This Month)"] = "Financial Overview (This Month)",
                    ["Total Revenue"] = "Total Revenue",
                    ["Gross Profit"] = "Gross Profit",
                    ["Expenses"] = "Expenses",
                    ["Net Profit"] = "Net Profit",
                    ["View All"] = "View All",
                    ["Alerts & Notifications"] = "Alerts & Notifications",
                    ["Open Purchase Orders"] = "Open Purchase Orders",
                    ["Materials To Order"] = "Materials To Order",
                    ["High Priority Tickets"] = "High Priority Tickets"
                },
                [Marathi] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Dashboard"] = "डॅशबोर्ड",
                    ["Clients"] = "ग्राहक",
                    ["Contracts"] = "AMC करार",
                    ["Invoices"] = "GST चलन",
                    ["Payments"] = "पेमेंट",
                    ["SLA Dashboard"] = "SLA डॅशबोर्ड",
                    ["Quotations"] = "कोटेशन",
                    ["Reports"] = "अहवाल",
                    ["Settings"] = "सेटिंग्ज",
                    ["Vendors"] = "विक्रेते",
                    ["Purchases"] = "खरेदी",
                    ["Inventory"] = "साहित्य",
                    ["Employees"] = "कर्मचारी",
                    ["Payroll"] = "पगार",
                    ["Dispatch Center"] = "डिस्पॅच केंद्र",
                    ["Jobs"] = "जॉब्स",
                    ["Retired"] = "निवृत्त",
                    ["Master Data"] = "मास्टर डेटा",
                    ["WhatsApp Hub"] = "WhatsApp केंद्र",
                    ["Tally"] = "Tally",
                    ["Settings & Support"] = "सेटिंग्ज आणि सपोर्ट",
                    ["Data & Compliance"] = "डेटा आणि अनुपालन",
                    ["HR & Payroll"] = "HR आणि पगार",
                    ["Operations"] = "ऑपरेशन्स",
                    ["Sales"] = "विक्री",
                    ["Save"] = "जतन करा",
                    ["Cancel"] = "रद्द करा",
                    ["Edit"] = "संपादित करा",
                    ["Delete"] = "हटवा",
                    ["Close"] = "बंद करा",
                    ["Add New"] = "नवीन जोडा",
                    ["Language"] = "भाषा",
                    ["Search"] = "शोध",
                    ["Alerts"] = "सूचना",
                    ["Logout"] = "लॉगआउट",
                    ["Change Password"] = "पासवर्ड बदला",
                    ["Help & Support"] = "मदत आणि सपोर्ट",
                    ["AI Copilot"] = "AI सहाय्यक",
                    ["Quick Action"] = "जलद कृती",
                    ["Renew license"] = "लायसन्स नूतनीकरण",
                    ["Install update"] = "अपडेट इन्स्टॉल करा",
                    ["Remind me later"] = "नंतर आठवण करा",
                    ["SearchPlaceholder"] = "ॲप्स, ग्राहक, जॉब्स, चलन, खरेदी शोधा...",
                    ["Customize"] = "कस्टमाईज",
                    ["Good"] = "शुभ",
                    ["morning"] = "सकाळ",
                    ["afternoon"] = "दुपार",
                    ["evening"] = "संध्याकाळ",
                    ["BusinessToday"] = "आज आपल्या व्यवसायात काय चालू आहे ते येथे दिसते.",
                    ["This Month"] = "हा महिना",
                    ["Last Month"] = "मागील महिना",
                    ["This Quarter"] = "ही तिमाही",
                    ["This Year"] = "हे वर्ष",
                    ["Sales / Quotations"] = "विक्री / कोटेशन",
                    ["Open Quotations"] = "उघडी कोटेशन",
                    ["Value (MTD)"] = "मूल्य (MTD)",
                    ["Jobs / Projects"] = "जॉब्स / प्रकल्प",
                    ["Total Active Jobs"] = "एकूण सक्रिय जॉब्स",
                    ["In Progress"] = "प्रगतीत",
                    ["Overdue"] = "मुदत ओलांडलेले",
                    ["Purchase Orders"] = "खरेदी ऑर्डर",
                    ["Open POs"] = "उघडे PO",
                    ["Overdue Invoices"] = "मुदत ओलांडलेली चलने",
                    ["Overdue Amount"] = "थकित रक्कम",
                    ["Pending Payables"] = "देय बाकी",
                    ["Net Cash Flow"] = "निव्वळ रोख प्रवाह",
                    ["Active Vendors"] = "सक्रिय विक्रेते",
                    ["Overdue Payables"] = "थकित देयके",
                    ["Active Clients"] = "सक्रिय ग्राहक",
                    ["Outstanding"] = "बाकी",
                    ["Materials / Procurement"] = "साहित्य / खरेदी",
                    ["To Order Items"] = "ऑर्डर करायचे आयटम",
                    ["Priced Items"] = "दर असलेले आयटम",
                    ["Active Employees"] = "सक्रिय कर्मचारी",
                    ["On Leave Today"] = "आज रजेवर",
                    ["Service Operations"] = "सेवा ऑपरेशन्स",
                    ["Open Service Tickets"] = "उघडी सेवा तिकिटे",
                    ["High Priority"] = "उच्च प्राधान्य",
                    ["Financial Overview (This Month)"] = "आर्थिक आढावा (हा महिना)",
                    ["Total Revenue"] = "एकूण महसूल",
                    ["Gross Profit"] = "एकूण नफा",
                    ["Expenses"] = "खर्च",
                    ["Net Profit"] = "निव्वळ नफा",
                    ["View All"] = "सर्व पहा",
                    ["Alerts & Notifications"] = "सूचना आणि अलर्ट",
                    ["Open Purchase Orders"] = "उघड्या खरेदी ऑर्डर",
                    ["Materials To Order"] = "ऑर्डरसाठी साहित्य",
                    ["High Priority Tickets"] = "उच्च प्राधान्य तिकिटे"
                },
                [Hindi] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Dashboard"] = "डैशबोर्ड",
                    ["Clients"] = "ग्राहक",
                    ["Contracts"] = "AMC अनुबंध",
                    ["Invoices"] = "GST इनवॉइस",
                    ["Payments"] = "भुगतान",
                    ["SLA Dashboard"] = "SLA डैशबोर्ड",
                    ["Quotations"] = "कोटेशन",
                    ["Reports"] = "रिपोर्ट",
                    ["Settings"] = "सेटिंग्स",
                    ["Vendors"] = "वेंडर",
                    ["Purchases"] = "खरीद",
                    ["Inventory"] = "सामग्री",
                    ["Employees"] = "कर्मचारी",
                    ["Payroll"] = "वेतन",
                    ["Dispatch Center"] = "डिस्पैच केंद्र",
                    ["Jobs"] = "जॉब्स",
                    ["Retired"] = "रिटायर्ड",
                    ["Master Data"] = "मास्टर डेटा",
                    ["WhatsApp Hub"] = "WhatsApp केंद्र",
                    ["Tally"] = "Tally",
                    ["Settings & Support"] = "सेटिंग्स और सपोर्ट",
                    ["Data & Compliance"] = "डेटा और अनुपालन",
                    ["HR & Payroll"] = "HR और वेतन",
                    ["Operations"] = "ऑपरेशन्स",
                    ["Sales"] = "बिक्री",
                    ["Save"] = "सेव करें",
                    ["Cancel"] = "रद्द करें",
                    ["Edit"] = "एडिट करें",
                    ["Delete"] = "हटाएं",
                    ["Close"] = "बंद करें",
                    ["Add New"] = "नया जोड़ें",
                    ["Language"] = "भाषा",
                    ["Search"] = "खोज",
                    ["Alerts"] = "अलर्ट",
                    ["Logout"] = "लॉगआउट",
                    ["Change Password"] = "पासवर्ड बदलें",
                    ["Help & Support"] = "मदद और सपोर्ट",
                    ["AI Copilot"] = "AI सहायक",
                    ["Quick Action"] = "त्वरित कार्य",
                    ["Renew license"] = "लाइसेंस रिन्यू करें",
                    ["Install update"] = "अपडेट इंस्टॉल करें",
                    ["Remind me later"] = "बाद में याद दिलाएं",
                    ["SearchPlaceholder"] = "ऐप, ग्राहक, जॉब्स, इनवॉइस, खरीद खोजें...",
                    ["Customize"] = "कस्टमाइज",
                    ["Good"] = "शुभ",
                    ["morning"] = "सुबह",
                    ["afternoon"] = "दोपहर",
                    ["evening"] = "शाम",
                    ["BusinessToday"] = "आज आपके व्यवसाय में क्या हो रहा है, यह यहां दिखता है.",
                    ["This Month"] = "यह महीना",
                    ["Last Month"] = "पिछला महीना",
                    ["This Quarter"] = "यह तिमाही",
                    ["This Year"] = "यह वर्ष",
                    ["Sales / Quotations"] = "बिक्री / कोटेशन",
                    ["Open Quotations"] = "ओपन कोटेशन",
                    ["Value (MTD)"] = "मूल्य (MTD)",
                    ["Jobs / Projects"] = "जॉब्स / प्रोजेक्ट",
                    ["Total Active Jobs"] = "कुल सक्रिय जॉब्स",
                    ["In Progress"] = "प्रगति में",
                    ["Overdue"] = "मुदत पार",
                    ["Purchase Orders"] = "खरीद ऑर्डर",
                    ["Open POs"] = "ओपन PO",
                    ["Overdue Invoices"] = "मुदत पार इनवॉइस",
                    ["Overdue Amount"] = "बकाया राशि",
                    ["Pending Payables"] = "देय बाकी",
                    ["Net Cash Flow"] = "नेट कैश फ्लो",
                    ["Active Vendors"] = "सक्रिय वेंडर",
                    ["Overdue Payables"] = "बकाया देय",
                    ["Active Clients"] = "सक्रिय ग्राहक",
                    ["Outstanding"] = "बकाया",
                    ["Materials / Procurement"] = "सामग्री / खरीद",
                    ["To Order Items"] = "ऑर्डर करने वाले आइटम",
                    ["Priced Items"] = "दर वाले आइटम",
                    ["Active Employees"] = "सक्रिय कर्मचारी",
                    ["On Leave Today"] = "आज छुट्टी पर",
                    ["Service Operations"] = "सेवा ऑपरेशन्स",
                    ["Open Service Tickets"] = "ओपन सेवा टिकट",
                    ["High Priority"] = "उच्च प्राथमिकता",
                    ["Financial Overview (This Month)"] = "आर्थिक सारांश (यह महीना)",
                    ["Total Revenue"] = "कुल आय",
                    ["Gross Profit"] = "सकल लाभ",
                    ["Expenses"] = "खर्च",
                    ["Net Profit"] = "नेट लाभ",
                    ["View All"] = "सभी देखें",
                    ["Alerts & Notifications"] = "अलर्ट और सूचनाएं",
                    ["Open Purchase Orders"] = "ओपन खरीद ऑर्डर",
                    ["Materials To Order"] = "ऑर्डर हेतु सामग्री",
                    ["High Priority Tickets"] = "उच्च प्राथमिकता टिकट"
                }
            };
            AddGlobalCommonStrings();
            AddPendingLocalisationStrings();
        }

        /// <summary>Sets the active UI language and notifies open screens.</summary>
        public static void SetLanguage(string language)
        {
            SetLanguage(language, true);
        }

        /// <summary>Sets the active UI language with optional notification.</summary>
        public static void SetLanguage(string language, bool raiseEvent)
        {
            string normalized = Normalize(language);
            if (string.Equals(CurrentLanguage, normalized, StringComparison.OrdinalIgnoreCase))
                return;

            CurrentLanguage = normalized;
            ApplyToOpenForms();
            if (raiseEvent)
                LanguageChanged?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>Returns the translated text for a key, falling back to English.</summary>
        public static string Get(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return string.Empty;

            Dictionary<string, string> languageMap;
            if (_strings.TryGetValue(CurrentLanguage, out languageMap) && languageMap.TryGetValue(key, out string translated))
                return translated;

            Dictionary<string, string> englishMap;
            if (_strings.TryGetValue(English, out englishMap) && englishMap.TryGetValue(key, out string fallback))
                return fallback;

            return key;
        }

        /// <summary>Returns true when the active language needs a Devanagari-capable font.</summary>
        public static bool IsDevanagariLanguage()
        {
            return string.Equals(CurrentLanguage, Marathi, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(CurrentLanguage, Hindi, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Returns the preferred UI font family for the active language.</summary>
        public static string GetUiFontFamily()
        {
            return IsDevanagariLanguage() ? "Nirmala UI" : "Segoe UI";
        }

        /// <summary>Applies translated text and the current language font to a control tree.</summary>
        public static void ApplyFont(Control parent)
        {
            if (parent == null)
                return;

            ApplyTextRecursive(parent);
            ApplyFontRecursive(parent, GetUiFontFamily());
        }

        /// <summary>Applies translated text and current language font to a newly added control tree.</summary>
        public static void ApplyControlTree(Control parent)
        {
            ApplyFont(parent);
        }

        /// <summary>Applies the active language to every currently open WinForms window.</summary>
        public static void ApplyToOpenForms()
        {
            foreach (Form form in Application.OpenForms.Cast<Form>().ToList())
            {
                if (form == null || form.IsDisposed)
                    continue;

                ApplyControlTree(form);
            }
        }

        private static void ApplyFontRecursive(Control control, string family)
        {
            if (control == null)
                return;

            if (control.Font != null && !string.Equals(control.Font.FontFamily.Name, "Segoe MDL2 Assets", StringComparison.OrdinalIgnoreCase))
                control.Font = new Font(family, control.Font.Size, control.Font.Style);

            foreach (Control child in control.Controls)
                ApplyFontRecursive(child, family);
        }

        private static void ApplyTextRecursive(Control control)
        {
            if (control == null)
                return;

            TranslateControlText(control);

            DataGridView grid = control as DataGridView;
            if (grid != null)
                TranslateGridHeaders(grid);

            TabControl tabs = control as TabControl;
            if (tabs != null)
                foreach (TabPage page in tabs.TabPages)
                    TranslateControlText(page);

            MenuStrip menu = control as MenuStrip;
            if (menu != null)
                foreach (ToolStripItem item in menu.Items)
                    TranslateToolStripItem(item);

            ToolStrip toolStrip = control as ToolStrip;
            if (toolStrip != null)
                foreach (ToolStripItem item in toolStrip.Items)
                    TranslateToolStripItem(item);

            ComboBox comboBox = control as ComboBox;
            if (comboBox != null)
                TranslateComboBoxItems(comboBox);

            foreach (Control child in control.Controls)
                ApplyTextRecursive(child);
        }

        private static void TranslateControlText(Control control)
        {
            if (control == null || string.IsNullOrWhiteSpace(control.Text))
                return;

            if (control is TextBox || control is RichTextBox || control is NumericUpDown || control is DateTimePicker)
                return;

            string original = OriginalFor(control, control.Text);
            control.Text = Get(original);
        }

        private static void TranslateGridHeaders(DataGridView grid)
        {
            foreach (DataGridViewColumn column in grid.Columns)
            {
                if (string.IsNullOrWhiteSpace(column.HeaderText))
                    continue;

                string original = OriginalFor(column, column.HeaderText);
                column.HeaderText = Get(original);
            }
        }

        private static void TranslateToolStripItem(ToolStripItem item)
        {
            if (item == null)
                return;

            if (!string.IsNullOrWhiteSpace(item.Text))
            {
                string original = OriginalFor(item, item.Text);
                item.Text = Get(original);
            }

            ToolStripDropDownItem dropDown = item as ToolStripDropDownItem;
            if (dropDown != null)
                foreach (ToolStripItem child in dropDown.DropDownItems)
                    TranslateToolStripItem(child);
        }

        private static void TranslateComboBoxItems(ComboBox comboBox)
        {
            if (comboBox == null || comboBox.Items.Count == 0)
                return;

            int selectedIndex = comboBox.SelectedIndex;
            for (int i = 0; i < comboBox.Items.Count; i++)
            {
                object item = comboBox.Items[i];
                if (item == null || item.GetType() != typeof(string))
                    continue;

                string original = OriginalFor(Tuple.Create(comboBox, i), item.ToString());
                comboBox.Items[i] = Get(original);
            }

            if (selectedIndex >= 0 && selectedIndex < comboBox.Items.Count)
                comboBox.SelectedIndex = selectedIndex;
        }

        private static string OriginalFor(object key, string currentText)
        {
            lock (_sync)
            {
                string original;
                if (_originalText.TryGetValue(key, out original))
                    return original;

                _originalText[key] = currentText ?? string.Empty;
                return currentText ?? string.Empty;
            }
        }

        private static void AddGlobalCommonStrings()
        {
            AddAll("New", "नवीन", "नया");
            AddAll("Add", "जोडा", "जोड़ें");
            AddAll("Update", "अपडेट करा", "अपडेट करें");
            AddAll("Refresh", "रिफ्रेश", "रिफ्रेश");
            AddAll("Export", "निर्यात", "निर्यात");
            AddAll("Import", "आयात", "आयात");
            AddAll("Print", "प्रिंट", "प्रिंट");
            AddAll("Preview", "पूर्वावलोकन", "पूर्वावलोकन");
            AddAll("View", "पहा", "देखें");
            AddAll("Open", "उघडा", "खोलें");
            AddAll("Clear", "साफ करा", "साफ करें");
            AddAll("Reset", "रीसेट", "रीसेट");
            AddAll("Submit", "सबमिट", "सबमिट");
            AddAll("Approve", "मंजूर", "स्वीकृत");
            AddAll("Reject", "नाकार", "अस्वीकार");
            AddAll("Yes", "होय", "हाँ");
            AddAll("No", "नाही", "नहीं");
            AddAll("OK", "ठीक आहे", "ठीक है");
            AddAll("Apply", "लागू करा", "लागू करें");
            AddAll("Back", "मागे", "पीछे");
            AddAll("Next", "पुढे", "आगे");
            AddAll("Finish", "पूर्ण", "समाप्त");
            AddAll("Browse", "ब्राउज", "ब्राउज");
            AddAll("Filter", "फिल्टर", "फिल्टर");
            AddAll("Filters", "फिल्टर", "फिल्टर");
            AddAll("Columns", "कॉलम", "कॉलम");
            AddAll("Actions", "कृती", "कार्य");
            AddAll("Status", "स्थिती", "स्थिति");
            AddAll("Priority", "प्राधान्य", "प्राथमिकता");
            AddAll("Type", "प्रकार", "प्रकार");
            AddAll("Date", "दिनांक", "दिनांक");
            AddAll("From", "पासून", "से");
            AddAll("To", "पर्यंत", "तक");
            AddAll("Name", "नाव", "नाम");
            AddAll("Phone", "फोन", "फोन");
            AddAll("Email", "ईमेल", "ईमेल");
            AddAll("Address", "पत्ता", "पता");
            AddAll("City", "शहर", "शहर");
            AddAll("State", "राज्य", "राज्य");
            AddAll("GSTIN", "GSTIN", "GSTIN");
            AddAll("PAN", "PAN", "PAN");
            AddAll("Amount", "रक्कम", "राशि");
            AddAll("Total", "एकूण", "कुल");
            AddAll("Balance", "बाकी", "बाकी");
            AddAll("Paid", "भरले", "भुगतान");
            AddAll("Pending", "प्रलंबित", "लंबित");
            AddAll("Completed", "पूर्ण", "पूर्ण");
            AddAll("Open Jobs", "उघडे जॉब्स", "ओपन जॉब्स");
            AddAll("All Jobs", "सर्व जॉब्स", "सभी जॉब्स");
            AddAll("Add New Job", "नवीन जॉब जोडा", "नया जॉब जोड़ें");
            AddAll("Job Number", "जॉब क्रमांक", "जॉब नंबर");
            AddAll("Job Title", "जॉब शीर्षक", "जॉब शीर्षक");
            AddAll("Client", "ग्राहक", "ग्राहक");
            AddAll("Assigned To", "नेमलेले", "असाइन किया गया");
            AddAll("Scheduled Date", "नियोजित दिनांक", "निर्धारित दिनांक");
            AddAll("Save Settings", "सेटिंग्ज जतन करा", "सेटिंग्स सेव करें");
            AddAll("Company Information", "कंपनी माहिती", "कंपनी जानकारी");
            AddAll("System Tools", "सिस्टम साधने", "सिस्टम टूल्स");
            AddAll("Backup & Recovery", "बॅकअप आणि रिकव्हरी", "बैकअप और रिकवरी");
            AddAll("Module Catalog", "मॉड्यूल कॅटलॉग", "मॉड्यूल कैटलॉग");
            AddAll("Compliance Export Pack", "अनुपालन निर्यात पॅक", "अनुपालन निर्यात पैक");
            AddAll("Open Source & Licenses", "ओपन सोर्स आणि लायसन्स", "ओपन सोर्स और लाइसेंस");
            AddAll("Legal Agreements", "कायदेशीर करार", "कानूनी समझौते");
        }

        private static void AddPendingLocalisationStrings()
        {
            AddAll("First page", "TODO(mr): First page", "TODO(hi): First page");
            AddAll("Previous page", "TODO(mr): Previous page", "TODO(hi): Previous page");
            AddAll("Next page", "TODO(mr): Next page", "TODO(hi): Next page");
            AddAll("Last page", "TODO(mr): Last page", "TODO(hi): Last page");
            AddAll("of", "TODO(mr): of", "TODO(hi): of");
            AddAll("Showing {0} to {1} of {2}", "TODO(mr): Showing {0} to {1} of {2}", "TODO(hi): Showing {0} to {1} of {2}");
            AddAll("An autosaved draft exists for this screen from {0}. Restore it?", "TODO(mr): An autosaved draft exists for this screen from {0}. Restore it?", "TODO(hi): An autosaved draft exists for this screen from {0}. Restore it?");
            AddAll("Autosave", "TODO(mr): Autosave", "TODO(hi): Autosave");
            AddAll("ServoERP - Backup & Recovery", "TODO(mr): ServoERP - Backup & Recovery", "TODO(hi): ServoERP - Backup & Recovery");
            AddAll("Backup & Recovery", "TODO(mr): Backup & Recovery", "TODO(hi): Backup & Recovery");
            AddAll("Backups stay on your own network, local machine, or external drive. ServoERP never sends client data to Harshal or servoerp.in.", "TODO(mr): Backups stay on your own network, local machine, or external drive. ServoERP never sends client data to Harshal or servoerp.in.", "TODO(hi): Backups stay on your own network, local machine, or external drive. ServoERP never sends client data to Harshal or servoerp.in.");
            AddAll("Network Server Connection", "TODO(mr): Network Server Connection", "TODO(hi): Network Server Connection");
            AddAll("Network Server Path (UNC)", "TODO(mr): Network Server Path (UNC)", "TODO(hi): Network Server Path (UNC)");
            AddAll("Test Connection", "TODO(mr): Test Connection", "TODO(hi): Test Connection");
            AddAll("Leave blank to skip network backup", "TODO(mr): Leave blank to skip network backup", "TODO(hi): Leave blank to skip network backup");
            AddAll("Local Backup Folder", "TODO(mr): Local Backup Folder", "TODO(hi): Local Backup Folder");
            AddAll("Schedule", "TODO(mr): Schedule", "TODO(hi): Schedule");
            AddAll("Run backup when app closes (if not already run today)", "TODO(mr): Run backup when app closes (if not already run today)", "TODO(hi): Run backup when app closes (if not already run today)");
            AddAll("Enable automatic backups", "TODO(mr): Enable automatic backups", "TODO(hi): Enable automatic backups");
            AddAll("Daily Backup Time", "TODO(mr): Daily Backup Time", "TODO(hi): Daily Backup Time");
            AddAll("Retention", "TODO(mr): Retention", "TODO(hi): Retention");
            AddAll("Keep backups for", "TODO(mr): Keep backups for", "TODO(hi): Keep backups for");
            AddAll("days (older backups will be deleted automatically)", "TODO(mr): days (older backups will be deleted automatically)", "TODO(hi): days (older backups will be deleted automatically)");
            AddAll("Manual Backup & Status", "TODO(mr): Manual Backup & Status", "TODO(hi): Manual Backup & Status");
            AddAll("Backup Now", "TODO(mr): Backup Now", "TODO(hi): Backup Now");
            AddAll("Open Backup Folder", "TODO(mr): Open Backup Folder", "TODO(hi): Open Backup Folder");
            AddAll("Backup Log", "TODO(mr): Backup Log", "TODO(hi): Backup Log");
            AddAll("Clear Log", "TODO(mr): Clear Log", "TODO(hi): Clear Log");
            AddAll("Backup settings saved.", "TODO(mr): Backup settings saved.", "TODO(hi): Backup settings saved.");
            AddAll("Network backup skipped.", "TODO(mr): Network backup skipped.", "TODO(hi): Network backup skipped.");
            AddAll("Connected", "TODO(mr): Connected", "TODO(hi): Connected");
            AddAll("Unreachable - check path or network", "TODO(mr): Unreachable - check path or network", "TODO(hi): Unreachable - check path or network");
            AddAll("Select the local ServoERP backup folder", "TODO(mr): Select the local ServoERP backup folder", "TODO(hi): Select the local ServoERP backup folder");
            AddAll("Creating manual backup...", "TODO(mr): Creating manual backup...", "TODO(hi): Creating manual backup...");
            AddAll("Backup failed: {0}", "TODO(mr): Backup failed: {0}", "TODO(hi): Backup failed: {0}");
            AddAll("Backup failed - please check settings", "TODO(mr): Backup failed - please check settings", "TODO(hi): Backup failed - please check settings");
            AddAll("Manual backup failed. Please check backup settings.", "TODO(mr): Manual backup failed. Please check backup settings.", "TODO(hi): Manual backup failed. Please check backup settings.");
            AddAll("Backup completed - saved to {0}", "TODO(mr): Backup completed - saved to {0}", "TODO(hi): Backup completed - saved to {0}");
            AddAll("Unknown backup failure.", "TODO(mr): Unknown backup failure.", "TODO(hi): Unknown backup failure.");
            AddAll("Could not open backup folder: {0}", "TODO(mr): Could not open backup folder: {0}", "TODO(hi): Could not open backup folder: {0}");
            AddAll("Backup log cleared.", "TODO(mr): Backup log cleared.", "TODO(hi): Backup log cleared.");
            AddAll("Success", "TODO(mr): Success", "TODO(hi): Success");
            AddAll("Failed", "TODO(mr): Failed", "TODO(hi): Failed");
            AddAll("Last backup: no successful backup found", "TODO(mr): Last backup: no successful backup found", "TODO(hi): Last backup: no successful backup found");
            AddAll("Last backup: {0} to {1}", "TODO(mr): Last backup: {0} to {1}", "TODO(hi): Last backup: {0} to {1}");
            AddAll("Network Server", "TODO(mr): Network Server", "TODO(hi): Network Server");
            AddAll("Local Folder", "TODO(mr): Local Folder", "TODO(hi): Local Folder");
            AddAll("External Drive", "TODO(mr): External Drive", "TODO(hi): External Drive");
            AddAll("backup destination", "TODO(mr): backup destination", "TODO(hi): backup destination");
            AddAll("Error Logs", "TODO(mr): Error Logs", "TODO(hi): Error Logs");
            AddAll("Clear Logs", "TODO(mr): Clear Logs", "TODO(hi): Clear Logs");
            AddAll("No crash logs found.", "TODO(mr): No crash logs found.", "TODO(hi): No crash logs found.");
            AddAll("Clear local crash logs?", "TODO(mr): Clear local crash logs?", "TODO(hi): Clear local crash logs?");
        }

        private static void AddAll(string key, string marathi, string hindi)
        {
            _strings[English][key] = key;
            _strings[Marathi][key] = marathi;
            _strings[Hindi][key] = hindi;
        }

        private static string Normalize(string language)
        {
            string value = (language ?? string.Empty).Trim();
            if (string.Equals(value, Marathi, StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Marathi", StringComparison.OrdinalIgnoreCase))
                return Marathi;

            if (string.Equals(value, Hindi, StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Hindi", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "हिंदी", StringComparison.OrdinalIgnoreCase))
                return Hindi;

            return English;
        }
    }
}
