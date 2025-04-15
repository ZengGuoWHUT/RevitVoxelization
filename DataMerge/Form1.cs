using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DataMerge
{
    public partial class Form1 : Form
    {
        private DataTable dt;
        public Form1()
        {
            InitializeComponent();
            dt = new DataTable();
            dt.Columns.Add("Name");
            dt.Columns.Add("Correct element");
            dt.Columns.Add("CR");
            dt.Columns.Add("Elements only redundant");
            dt.Columns.Add("RR");
            dt.Columns.Add("Elements only missing");
            dt.Columns.Add("MR");
            dt.Columns.Add("elements both");
            dt.Columns.Add("BR");
            dataGridView1.DataSource = dt;
        }

        private void btnLoad_Click(object sender, EventArgs e)
        {
            List<string> lstFileNames = new List<string>();
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Multiselect = true;
            if(DialogResult.OK == ofd.ShowDialog())
            {
                foreach (var filename in ofd.FileNames)
                {
                    lstFileNames.Add(filename);
                }
            }
            foreach (var fn in lstFileNames)
            {
                string strElemName = fn.Split('\\').Last();
                StreamReader sw = new StreamReader(fn);
                sw.ReadLine();
                int numCorrectElem = 0;
                int numRedElem = 0;
                int numMissingElem = 0;
                int numBothElem = 0;
                
                while(!sw.EndOfStream)
                {
                    string[] strData = sw.ReadLine().Split (',');
                    string strMissing = strData[3].Trim();
                    string strRed = strData[4].Trim();
                    if(strMissing =="0" && strRed !="0")
                    {
                        numRedElem += 1;
                    }
                    else if(strMissing != "0" && strRed != "0")
                    {
                        numBothElem += 1;
                    }
                    else if(strMissing != "0" && strRed == "0")
                    {
                        numMissingElem += 1;
                    }
                    else
                    {
                        numCorrectElem += 1;
                    }
                }
                sw.Close();
                int elemCount = numRedElem + numBothElem + numMissingElem + numCorrectElem;
                double dblRatioCorrect = (double)numCorrectElem / elemCount*100;
                double dblRatioMissing=(double)numMissingElem / elemCount*100;
                double dblRatioRedunant=(double)numRedElem / elemCount*100;
                double dblRatioBoth=(double)numBothElem / elemCount*100;
                dt.Rows.Add(strElemName,
                                numCorrectElem,
                                dblRatioCorrect.ToString("0.00"),
                                numRedElem,
                                dblRatioRedunant.ToString("0.00"),
                                numMissingElem,
                                dblRatioMissing.ToString("0.00"),
                                numBothElem,
                                dblRatioBoth.ToString("0.00"));
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfg = new SaveFileDialog();
            sfg.Filter = "csv|*.csv";
            if(sfg.ShowDialog() == DialogResult.OK)
            {
                StreamWriter sw=new StreamWriter (sfg.FileName);
                List<string> colName = new List<string>();
                foreach (DataColumn dc in dt.Columns)
                {
                    colName.Add(dc.ColumnName);
                }
                sw.WriteLine(String.Join (",",colName));
                foreach (DataRow row in dt.Rows)
                {
                    sw.WriteLine(String.Join(",", row.ItemArray));
                }
                sw.Flush();
                sw.Close();
                MessageBox.Show("保存成功");
                Process.Start("Explorer", "/select," + sfg.FileName);
            }
        }
    }
}
