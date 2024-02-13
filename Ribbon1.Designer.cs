namespace plinxl
{
    partial class Ribbon1 : Microsoft.Office.Tools.Ribbon.RibbonBase
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        public Ribbon1()
            : base(Globals.Factory.GetRibbonFactory())
        {
            InitializeComponent();
        }

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.PlixTab = this.Factory.CreateRibbonTab();
            this.group1 = this.Factory.CreateRibbonGroup();
            this.reset = this.Factory.CreateRibbonButton();
            this.consultWS = this.Factory.CreateRibbonButton();
            this.groupTrace = this.Factory.CreateRibbonGroup();
            this.consoleOnOff = this.Factory.CreateRibbonToggleButton();
            this.tracer = this.Factory.CreateRibbonCheckBox();
            this.groupTracking = this.Factory.CreateRibbonGroup();
            this.backtrackEnd = this.Factory.CreateRibbonButton();
            this.backtrackNext = this.Factory.CreateRibbonButton();
            this.group2 = this.Factory.CreateRibbonGroup();
            this.about = this.Factory.CreateRibbonButton();
            this.plixWeb = this.Factory.CreateRibbonButton();
            this.PlixTab.SuspendLayout();
            this.group1.SuspendLayout();
            this.groupTrace.SuspendLayout();
            this.groupTracking.SuspendLayout();
            this.group2.SuspendLayout();
            this.SuspendLayout();
            // 
            // PlixTab
            // 
            this.PlixTab.ControlId.ControlIdType = Microsoft.Office.Tools.Ribbon.RibbonControlIdType.Office;
            this.PlixTab.Groups.Add(this.group1);
            this.PlixTab.Groups.Add(this.groupTrace);
            this.PlixTab.Groups.Add(this.groupTracking);
            this.PlixTab.Groups.Add(this.group2);
            this.PlixTab.Label = "Plinxl";
            this.PlixTab.Name = "PlixTab";
            // 
            // group1
            // 
            this.group1.Items.Add(this.reset);
            this.group1.Items.Add(this.consultWS);
            this.group1.Name = "group1";
            // 
            // reset
            // 
            this.reset.Label = "reset";
            this.reset.Name = "reset";
            this.reset.OfficeImageId = "DataRefreshDialog";
            this.reset.ScreenTip = "reset ALL the clauses.";
            this.reset.ShowImage = true;
            this.reset.SuperTip = "Reset all the current clauses wherever they come from: consultation of one or mul" +
    "tiples worksheets, or added by \"assert\", ...";
            this.reset.Click += new Microsoft.Office.Tools.Ribbon.RibbonControlEventHandler(this.reset_Click);
            // 
            // consultWS
            // 
            this.consultWS.Label = "?- consult.";
            this.consultWS.Name = "consultWS";
            this.consultWS.OfficeImageId = "FilesToolDownloadMenu";
            this.consultWS.ScreenTip = "consult the current worksheet.";
            this.consultWS.ShowImage = true;
            this.consultWS.SuperTip = "Reset previous program. Then consult the clauses in the current worksheet only.";
            this.consultWS.Click += new Microsoft.Office.Tools.Ribbon.RibbonControlEventHandler(this.consultWS_Click);
            // 
            // groupTrace
            // 
            this.groupTrace.Items.Add(this.consoleOnOff);
            this.groupTrace.Items.Add(this.tracer);
            this.groupTrace.Name = "groupTrace";
            // 
            // consoleOnOff
            // 
            this.consoleOnOff.Label = "console";
            this.consoleOnOff.Name = "consoleOnOff";
            this.consoleOnOff.OfficeImageId = "BibliographyManageSources";
            this.consoleOnOff.ScreenTip = "Display the plinxl console if not already visible";
            this.consoleOnOff.ShowImage = true;
            this.consoleOnOff.Click += new Microsoft.Office.Tools.Ribbon.RibbonControlEventHandler(this.consoleOnOff_Click);
            // 
            // tracer
            // 
            this.tracer.Label = "trace";
            this.tracer.Name = "tracer";
            this.tracer.ScreenTip = "Require the \'trace\' mode.";
            this.tracer.SuperTip = "Same as the trace/0 and notrace/0 predicates";
            // 
            // groupTracking
            // 
            this.groupTracking.Items.Add(this.backtrackEnd);
            this.groupTracking.Items.Add(this.backtrackNext);
            this.groupTracking.Name = "groupTracking";
            // 
            // backtrackEnd
            // 
            this.backtrackEnd.Label = "end.";
            this.backtrackEnd.Name = "backtrackEnd";
            this.backtrackEnd.OfficeImageId = "ConnectionPointTool";
            this.backtrackEnd.ScreenTip = "Terminate the current query.";
            this.backtrackEnd.ShowImage = true;
            this.backtrackEnd.SuperTip = "You may also click on \".\" or \"esc\" inside the console input zone.";
            this.backtrackEnd.Click += new Microsoft.Office.Tools.Ribbon.RibbonControlEventHandler(this.backtrackEnd_Click);
            // 
            // backtrackNext
            // 
            this.backtrackNext.Label = "next;";
            this.backtrackNext.Name = "backtrackNext";
            this.backtrackNext.OfficeImageId = "CatalogMergeGoToNextRecord";
            this.backtrackNext.ScreenTip = "Backtrack to the next answer of the current query";
            this.backtrackNext.ShowImage = true;
            this.backtrackNext.SuperTip = "You may also click on \";\" or \"return\" or \"space\" inside the console input zone.";
            this.backtrackNext.Click += new Microsoft.Office.Tools.Ribbon.RibbonControlEventHandler(this.backtrackNext_Click);
            // 
            // group2
            // 
            this.group2.Items.Add(this.about);
            this.group2.Items.Add(this.plixWeb);
            this.group2.Name = "group2";
            // 
            // about
            // 
            this.about.Label = "About...";
            this.about.Name = "about";
            this.about.OfficeImageId = "JotShareMenuHelp";
            this.about.ScreenTip = "Get the current version of this plinxl add-in.";
            this.about.ShowImage = true;
            this.about.SuperTip = "Plinxl version";
            this.about.Click += new Microsoft.Office.Tools.Ribbon.RibbonControlEventHandler(this.about_Click);
            // 
            // plixWeb
            // 
            this.plixWeb.Label = "plinxl.com";
            this.plixWeb.Name = "plixWeb";
            this.plixWeb.OfficeImageId = "HelpContactMicrosoft";
            this.plixWeb.ShowImage = true;
            this.plixWeb.SuperTip = "Go to plinxl website.";
            this.plixWeb.Click += new Microsoft.Office.Tools.Ribbon.RibbonControlEventHandler(this.plixWeb_Click);
            // 
            // Ribbon1
            // 
            this.Name = "Ribbon1";
            this.RibbonType = "Microsoft.Excel.Workbook";
            this.Tabs.Add(this.PlixTab);
            this.Load += new Microsoft.Office.Tools.Ribbon.RibbonUIEventHandler(this.Ribbon1_Load);
            this.PlixTab.ResumeLayout(false);
            this.PlixTab.PerformLayout();
            this.group1.ResumeLayout(false);
            this.group1.PerformLayout();
            this.groupTrace.ResumeLayout(false);
            this.groupTrace.PerformLayout();
            this.groupTracking.ResumeLayout(false);
            this.groupTracking.PerformLayout();
            this.group2.ResumeLayout(false);
            this.group2.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        internal Microsoft.Office.Tools.Ribbon.RibbonTab PlixTab;
        internal Microsoft.Office.Tools.Ribbon.RibbonGroup group1;
        internal Microsoft.Office.Tools.Ribbon.RibbonButton reset;
        internal Microsoft.Office.Tools.Ribbon.RibbonButton consultWS;
        internal Microsoft.Office.Tools.Ribbon.RibbonGroup group2;
        internal Microsoft.Office.Tools.Ribbon.RibbonButton about;
        internal Microsoft.Office.Tools.Ribbon.RibbonButton plixWeb;
        internal Microsoft.Office.Tools.Ribbon.RibbonGroup groupTrace;
        internal Microsoft.Office.Tools.Ribbon.RibbonToggleButton consoleOnOff;
        internal Microsoft.Office.Tools.Ribbon.RibbonGroup groupTracking;
        internal Microsoft.Office.Tools.Ribbon.RibbonButton backtrackNext;
        internal Microsoft.Office.Tools.Ribbon.RibbonButton backtrackEnd;
        internal Microsoft.Office.Tools.Ribbon.RibbonCheckBox tracer;
    }

    partial class ThisRibbonCollection
    {
        internal Ribbon1 Ribbon1
        {
            get { return this.GetRibbon<Ribbon1>(); }
        }
    }
}