using Microsoft.VisualBasic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Diagnostics;
using System.Windows.Forms;

//using System.Math;
using System.IO;
using System.Xml;

using ApsimFile;
using Controllers;
using CSGeneral;
using Steema.TeeChart.Styles;
namespace CSUserInterface
{

    public partial class MetGraphControl : BaseView
    {

        private APSIMInputFile Metfile = new APSIMInputFile();
        private DataTable MetData = new DataTable();
        private DataTable MonthlyData = new DataTable();
        private DataView YearlyData;
        private DateTime StartDate;
        private DateTime EndDate;
        private string FileName;

        public MetGraphControl()
            : base()
        {
            InitializeComponent();
        }

        public override void OnRefresh()
        {
            ContentsBox.Text = "";

            string FullFileName = Controller.ToAbsolute(FileName);
            if (File.Exists(FullFileName))
            {
                MetData = new DataTable();
                MetData.TableName = "Met";
                Metfile.ReadFromFile(FullFileName, MetData);
                StartDate = DataTableUtility.GetDateFromRow(MetData.Rows[0]);
                EndDate = DataTableUtility.GetDateFromRow(MetData.Rows[MetData.Rows.Count - 1]);
                PopulateRawData();
                YearStartBox.ValueChanged -= YearStartBoxChanged;
                NumYearsBox.ValueChanged -= NumYearsBoxChanged;
                YearStartBox.Value = StartDate.Year;
                NumYearsBox.Value = 1;
                YearStartBox.ValueChanged += YearStartBoxChanged;
                NumYearsBox.ValueChanged += NumYearsBoxChanged;
                RefreshAllCharts();
            }
            YearPanel.Visible = (TabControl.SelectedIndex != 0);
            YearPanel.Parent = this;
            YearPanel.BringToFront();
        }


        public void SetFileName(string FileName)
        {
            FileName = Controller.ToRelativePath(FileName);
            if (this.FileName != FileName)
            {
                XmlHelper.SetValue(Data, "filename", FileName);
                this.FileName = FileName;
                OnRefresh();
            }
        }
        public string GetFileName()
        {
            return this.FileName;
        }
        private void YearStartBoxChanged(object sender, System.EventArgs e)
        {
            RefreshAllCharts();
        }
        private void NumYearsBoxChanged(System.Object sender, System.EventArgs e)
        {
            RefreshAllCharts();
        }
        private void TabControl_TabIndexChanged(System.Object sender, System.EventArgs e)
        {
            YearPanel.Visible = (TabControl.SelectedIndex != 0);
        }

        private void PopulateRawData()
        {
            StreamReader sr = new StreamReader(Controller.ToAbsolute(FileName));
            ContentsBox.Text = sr.ReadToEnd();
            sr.Close();
        }

        private void RefreshAllCharts()
        {
            // ----------------------------------------------------------------------------------
            // Refresh all data for current year and attach data to lines and bars on chart.
            // ----------------------------------------------------------------------------------

            if (MetData.Rows.Count > 0)
            {
                YearlyData = DataTableUtility.FilterTableForYear(MetData, (int)YearStartBox.Value, (int)YearStartBox.Value + (int)NumYearsBox.Value - 1);

                //JF 061211 - Fix bug in max radiation for years that don't begin at day 1 by sending the starting day to QMax
                float firstDay = 0;
                if ((YearlyData.Count > 0))
                {
                    firstDay = (float)YearlyData[0]["day"];
                }

                if (YearlyData.Table.Columns.IndexOf("Rain") != -1)
                {
                    double[] Rainfall = DataTableUtility.ColumnValues(YearlyData, "rain");
                    if (NumYearsBox.Value == 1)
                    {
                        RainfallLabel.Text = MathUtility.Sum(Rainfall).ToString("f1") + " mm for the year " + YearStartBox.Value.ToString();
                    }
                    else
                    {
                        RainfallLabel.Text = MathUtility.Sum(Rainfall).ToString("f1") + " mm for the years " + YearStartBox.Value.ToString() + " to " + (YearStartBox.Value + NumYearsBox.Value - 1).ToString();
                    }

                }
                else
                {
                    RainfallLabel.Text = "";
                }
                MonthlyData = DataTableUtility.MonthlySums(YearlyData);
                CalcQmax(firstDay);
                PopulateSeries(RainfallBar, YearlyData, "Rain");
                PopulateSeries(RainfallBar2, YearlyData, "Rain");
                PopulateSeries(MaximumTemperatureLine, YearlyData, "MaxT");
                PopulateSeries(MinimumTemperatureLine, YearlyData, "MinT");
                PopulateSeries(RadiationLine, YearlyData, "Radn");
                PopulateSeries(MaximumRadiationLine, YearlyData, "QMax");
                PopulateSeries(MonthlyRainfallBar, MonthlyData, "Rain");
                if (MonthlyData.Columns.IndexOf("pan") != -1)
                {
                    PopulateSeries(MonthlyEvaporationLine, MonthlyData, "pan");
                }
                else
                {
                    PopulateSeries(MonthlyEvaporationLine, MonthlyData, "Evap");
                }
            }
        }


        private void PopulateSeries(Series RainfallBar, DataView Data, string ColumnName)
        {
            RainfallBar.Clear();
            if (Data.Table.Columns.IndexOf(ColumnName) != -1)
            {
                for (int Row = 0; Row <= Data.Count - 1; Row++)
                {
                    DateTime D = DataTableUtility.GetDateFromRow(Data[Row].Row);
                    RainfallBar.Add(D, Convert.ToDouble(Data[Row][ColumnName]));
                }
            }
        }

        private void PopulateSeries(Series RainfallBar, DataTable Data, string ColumnName)
        {
            RainfallBar.Clear();
            if (Data.Columns.IndexOf(ColumnName) != -1 && Data.Rows.Count > 0 && !Convert.IsDBNull(Data.Rows[0][ColumnName]))
            {
                for (int Row = 0; Row <= Data.Rows.Count - 1; Row++)
                {
                    DateTime D = DataTableUtility.GetDateFromRow(Data.Rows[Row]);
                    RainfallBar.Add(D, Convert.ToDouble(Data.Rows[Row][ColumnName]));
                }
            }
        }


        private void CalcQmax(float firstDay)
        {
            // ----------------------------------------------------------------------------------
            // Add a calculated QMax column to the daily data.
            // ----------------------------------------------------------------------------------
            if (((MetData.Columns["Qmax"] == null)))
            {
                MetData.Columns.Add("Qmax");
            }

            // Do we have a VP column?
            bool HaveVPColumn = (MetData.Columns["VP"] != null);

            // Get latitude for later on.
            float Latitude = (float)Convert.ToDouble(Metfile.Constant("latitude").Value, new System.Globalization.CultureInfo("en-US"));

            // Loop through all rows and calculate a QMax
            int doy = Convert.ToInt32(firstDay);
            for (int Row = 0; Row <= YearlyData.Count - 1; Row++)
            {
                doy = doy + 1;
                if (HaveVPColumn && !Convert.IsDBNull(YearlyData[Row]["vp"]))
                {
                    YearlyData[Row]["Qmax"] = MetUtility.QMax(doy + 1, Latitude, MetUtility.Taz, MetUtility.Alpha, (float)YearlyData[Row]["vp"]);
                }
                else
                {
                    YearlyData[Row]["Qmax"] = MetUtility.QMax(doy + 1, Latitude, MetUtility.Taz, MetUtility.Alpha, MetUtility.svp((float)YearlyData[Row]["mint"]));
                }
            }

        }

    }
}