using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using RevitVoxelzation;
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

namespace RevitVoxelzation
{
    public partial class FrmCompressMesh : System.Windows.Forms.Form
    {
        public FrmCompressMesh()
        {
            InitializeComponent();
        }
        private BackgroundWorker worker;
        private VoxelDocument voxDoc;
        private string filePath;
        
        private void btnCompress_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Multiselect = true;
            ofd.Filter = "voxel files|*.vox";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                string[] strFileName = ofd.FileNames;
                voxDoc = VoxelDocumentConverter.LoadVoxelDocuments(ofd.FileNames);

                //convert 
                //var cvdoc = LEGOVoxelTool.CompressVoxelDocuments(voxDoc);
                //visualize
                //save
                SaveFileDialog sfg = new SaveFileDialog();
                sfg.Filter = "compressed voxel files|*.cvox";
                if (sfg.ShowDialog() == DialogResult.OK && sfg.FileName != String.Empty)
                {
                    worker = new BackgroundWorker();
                    worker.WorkerReportsProgress = true;
                    worker.WorkerSupportsCancellation = true;
                    worker.DoWork += Worker_DoWork;
                    worker.ProgressChanged += Worker_ProgressChanged;
                    worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
                    progCompress.Minimum = 0;
                    progCompress.Maximum = voxDoc.Elements.Count;
                    progCompress.Value = 0;
                    filePath =sfg.FileName;
                    worker.RunWorkerAsync(filePath);
                }
            }
        }

        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if(e.Error==null)
            {
                MessageBox.Show("File saved successfully");
                Process.Start("Explorer", "/select," + filePath);
            }
            else
            {
                MessageBox.Show(e.Error.Message + e.Error.StackTrace);
            }
        }

        private void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            progCompress.Value += 1;
            this.Text = string.Format("{0} Done, {1} remaining", e.UserState.ToString(), progCompress.Maximum - progCompress.Value);
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            var filePath =e.Argument.ToString ();
            var tempFilePath = Path.GetTempFileName();
            var fsTemp = new FileStream(tempFilePath, FileMode.Create);
            var fs = new FileStream(filePath, FileMode.Create);
            BinaryWriter bw = new BinaryWriter(fsTemp);
            try
            {
                Dictionary<CellIndex3D, int> dicScales = new Dictionary<CellIndex3D, int>();                                            //write elem
                foreach (var ve in voxDoc.Elements)
                {
                    var rects = LEGOVoxelTool.CompressVoxels(ref dicScales, ve);
                    var compElem = new CompressedVoxelElement(ve, rects);
                    bw.Write(LEGOVoxelTool.CompressedVoxelElems2Bytes(compElem));
                    string strName = ve.Name;
                    worker.ReportProgress(0, strName);
                }
                bw.Flush();
                bw.Close();
                fsTemp = new FileStream(tempFilePath, FileMode.Open);
                List<CellIndex3D> scales = dicScales.Keys.ToList();
                bw = new BinaryWriter(fs);
                //write voxel info
                bw.Write(LEGOVoxelTool.Vec32Byte(voxDoc.Origin));//origin
                bw.Write(BitConverter.GetBytes(voxDoc.VoxelSize));//voxel size;
                                                                  //write scaels
                bw.Write(scales.Count);
                foreach (var item in scales)
                {
                    bw.Write(LEGOVoxelTool.CellIndex3D2Byte(item));
                }
                //write element count
                byte[] elemCoumt = BitConverter.GetBytes(voxDoc.Elements.Count);
                bw.Write(voxDoc.Elements.Count);
                //write element
                byte[] buffer = new byte[1024];
                long len = fsTemp.Length;
                int bytesRead = fsTemp.Read(buffer, 0, buffer.Length);

                while (bytesRead > 0)
                {
                    bw.Write(buffer, 0, bytesRead);
                    bytesRead = fsTemp.Read(buffer, 0, buffer.Length);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                bw.Flush();
                bw.Close();
                fs.Close();
                fsTemp.Close();
                File.Delete(tempFilePath);
            }
            
        }
        private CompressedVoxelDocument compVoxelDoc = null;
        private void button1_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Multiselect = true;
            ofd.Filter = "Compressed voxel files|*.cvox";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    string[] strFileName = ofd.FileNames;
                    var cvdoc = LEGOVoxelTool.LoadCompressedVoxelDocument(ofd.FileName);
                    var voxSize = cvdoc.VoxelScale;
                    int numRects = 0;
                    compVoxelDoc = cvdoc;
                    foreach (var elem in cvdoc.Elements)
                    {
                        foreach (var rect in elem.VoxelRectangles)
                        {
                            numRects += 1;
                        }
                    }
                    MessageBox.Show(string.Format("Number of Rectangles:{0},same rectangles:{1}", numRects, voxSize.Count));
                    LoadCompressedVoxelFiles.cvdoc=cvdoc;
                    var frmBase = this.Owner as FrmVoxel;
                    if(MessageBox.Show ("Visualize Voxelized Element？","Voxelize",MessageBoxButtons.YesNo)==DialogResult.Yes)
                    {
                        this.Close();
                        frmBase.ShowCompressedVoxel.Raise();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message + ex.StackTrace);
                }
            }
        }

        private void FrmCompressMesh_Load(object sender, EventArgs e)
        {

        }
        private List<AccessibleRegion> accessibleRngs;
        private void btnGenARect_Click(object sender, EventArgs e)
        {
            Stopwatch stopwatch = new Stopwatch();
            //param 
            double dblObsAffRng = 0 / 304.8;
            double dblStrideHeight = 400 / 304.8;
            double dblMinPassingHeight = 1500 / 304.8;
            int bigCellInterval = 10;// the buffer is 10 times that of the voxel size
            //create a voxelRectManager to fast search rectangles
            VoxelRectangleManager manager = new VoxelRectangleManager(compVoxelDoc, bigCellInterval);
            //use a list to save accessible rectangle
            var supArs = new List<AccessibleRectangle>();
            int obsAffRng = (int)Math.Ceiling(Math.Round(dblObsAffRng / manager.Doc.VoxelSize, 4));
            stopwatch.Start();
            foreach (var supRect in manager.SupRects)
            {
                supArs.AddRange(LEGOVoxelTool.GenereateAccessibleRectangles4SupportRect(supRect, manager, dblStrideHeight, dblMinPassingHeight, obsAffRng));
            }
            stopwatch.Stop();
            string timeGenAR = stopwatch.Elapsed.TotalSeconds.ToString("0.000");
            stopwatch.Restart();
            LEGOVoxelTool.FindAccessibleRectangleNeighbors(supArs, dblStrideHeight, bigCellInterval);
            stopwatch.Stop();
            string timeLinking = stopwatch.Elapsed.TotalSeconds.ToString("0.000");
            stopwatch.Restart();
            accessibleRngs = AccessibleRegion.GenerateAccessibleRegions(supArs);
            stopwatch.Stop();
            string timeGenAr= stopwatch.Elapsed.TotalSeconds.ToString("0.000");
            var frmBase = this.Owner as FrmVoxel;
            ShowRawAccessibleRegion.Ars2Show = supArs;
            ShowRawAccessibleRegion.cvdoc = this.compVoxelDoc;
            ShowRegionHandler.Doc = compVoxelDoc;
            ShowRegionHandler.Regions = accessibleRngs;
            if (MessageBox.Show(String.Format("time elapsed for generating ARs：{0};\r\n " +
                                                "Linking ARs:{1};\r\n " +
                                                "Generate Accessible Region{2}"
                                                , timeGenAR,timeLinking,timeGenAr)+  
                                                "Visualize result?","Caution",MessageBoxButtons.YesNo )==DialogResult.Yes)
            {
                frmBase.ShowAccessibleRegion.Raise();
                //frmBase.ShowAccessibleRectangle.Raise();
            }
            //search obstruction elem Nearby
        }

        private void btnSaveAR_Click(object sender, EventArgs e)
        {
            //save
            if(this.compVoxelDoc==null)
            {
                MessageBox.Show("No document loaded");
                return;
            }
            if(this.accessibleRngs ==null)
            {
                MessageBox.Show("No accessible region loaded");
                return;
            }
            SaveFileDialog sfg = new SaveFileDialog();
            sfg.Filter = "AccessibleRegion files|*.accessibleRng";
            if (sfg.ShowDialog() == DialogResult.OK)
            {
                string saveFilePath = sfg.FileName;
                var vdoc = this.compVoxelDoc;
                var regions2Save = accessibleRngs;
                //file format:
                //1. Origin:24bytes;
                List<byte> data = new List<byte>() { Capacity = 24 };
                data.AddRange(BitConverter.GetBytes(vdoc.Origin.X));
                data.AddRange(BitConverter.GetBytes(vdoc.Origin.Y));
                data.AddRange(BitConverter.GetBytes(vdoc.Origin.Z));
                //2. VoxelSize: 8 bytes
                data.AddRange(BitConverter.GetBytes(vdoc.VoxelSize));
                //3. AccessibRegions, the sequense refers to the number of rectangles
                data.AddRange(BitConverter.GetBytes(regions2Save.Count));
                foreach (var rng in regions2Save)
                {
                    //3.1 numAccessibleRectangles
                    int numRectsInRng = rng.Rectangles.Count;
                    data.AddRange(BitConverter.GetBytes(numRectsInRng));
                    //3.2 AccessibleRectangles in current rng
                    foreach (var rect in rng.Rectangles)
                    {
                        //3.2.1 min
                        data.AddRange(BitConverter.GetBytes(rect.Min.Col));
                        data.AddRange(BitConverter.GetBytes(rect.Min.Row));
                        //3.2.2 max
                        data.AddRange(BitConverter.GetBytes(rect.Max.Col));
                        data.AddRange(BitConverter.GetBytes(rect.Max.Row));
                        //3.2.3 elev
                        data.AddRange(BitConverter.GetBytes(rect.Elevation));
                        //3.2.4 numAdjRects
                        data.AddRange(BitConverter.GetBytes(rect.AdjacentRectangles.Count));
                        //3.2.5-3.2.n,each adjRect.Index
                        foreach (var rectAdj in rect.AdjacentRectangles)
                        {
                            data.AddRange(BitConverter.GetBytes(rectAdj.Index));
                        }
                    }
                }
                //save file
                FileStream fs = new FileStream(saveFilePath, FileMode.OpenOrCreate);
                fs.Write(data.ToArray(), 0, data.Count);
                fs.Flush();
                fs.Close();

                MessageBox.Show("可达区域保存成功");
                Process.Start("Explorer", "/select," + sfg.FileName);
            }

        }
        private AccessibeDocument accDoc;
        private void btnPathPlanning_Click(object sender, EventArgs e)
        {
            var uiDoc = Revit2LEGO.UIDocument;
            var sel = uiDoc.Selection;
            var elemRef = sel.PickObject(ObjectType.Element, new RPCFilter(), "Please select start");
            var elemSt = uiDoc.Document.GetElement(elemRef);
            var boxStart = elemSt.get_BoundingBox(uiDoc.Document.ActiveView);
            var startPt = (boxStart.Max +boxStart.Min)/2 ;
            var stPt = new Vec3(startPt.X, startPt.Y, startPt.Z);
            elemRef = sel.PickObject(ObjectType.Element, new RPCFilter(), "Please select target");
            var elemEd = uiDoc.Document.GetElement(elemRef);
            var boxEd = elemEd.get_BoundingBox(uiDoc.Document.ActiveView);
            var endPt = (boxEd.Min + boxEd.Max) / 2;
            var edPt = new Vec3(endPt.X, endPt.Y, endPt.Z);
            this.Text =String.Format ("Start:{0};Target:{1}",stPt.ToString (),edPt.ToString());
            Stopwatch sw = new Stopwatch();
            sw.Start();
            if (PathPlanningTool.PathPlanning(stPt, edPt, accDoc,out var pathPoints))
            {
                sw.Stop();
                MessageBox.Show("Path found! time elapsed(ms):" + sw.Elapsed.TotalMilliseconds.ToString());
                FrmVoxel owner = this.Owner as FrmVoxel;
                ShowPathHandler.Path = pathPoints;
                owner.ShowPath.Raise();
            }
            else
            {
                sw.Stop();
                MessageBox.Show("No path found");
            }
        }

        private void btnLoadRegion_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Accessible region|*.accessibleRng";
            if(ofd.ShowDialog() == DialogResult.OK)
            {
                this.accDoc = PathPlanningTool.LoadAccessibleRegionDocument(ofd.FileName);
                double dblAreaM2 = accDoc.GetArea(AccessibeDocument.AreaUnit.SquareMeter);
                MessageBox.Show("Accessible region loaded successfully,Area(m2):"+dblAreaM2.ToString("0.000"));
            }
        }
    }

    public class RPCFilter : ISelectionFilter
    {
        public bool AllowElement(Autodesk.Revit.DB.Element elem)
        {
            return (elem.Category.Id == new Autodesk.Revit.DB.ElementId(Autodesk.Revit.DB.BuiltInCategory.OST_Entourage));
            //throw new NotImplementedException();
        }

        public bool AllowReference(Autodesk.Revit.DB.Reference reference, Autodesk.Revit.DB.XYZ position)
        {
            return true;
            //throw new NotImplementedException();
        }
    }
}
