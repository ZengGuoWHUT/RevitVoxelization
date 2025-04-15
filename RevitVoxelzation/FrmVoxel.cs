using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Form = System.Windows.Forms.Form;
using TextBox = System.Windows.Forms.TextBox;

namespace RevitVoxelzation
{
    public partial class FrmVoxel : Form
    {
        public ExternalEvent GenerateVoxels { get; set; }
        public ExternalEvent VisualiezVoxels { get; set; }
        public ExternalEvent DeletePath { get; internal set; }
        public ExternalEvent VisualizeRegions { get; set; }
        public ExternalEvent VisualiezSingleVoxels { get; internal set; }
        public ExternalEvent ShowTriangle { get; internal set; }
        public ExternalEvent ShowValidationResult { get; set; }
        public MeshDocument MeshDoc { get; set; }

        public HashSet<string> SupportElemIds { get; set; }

        public HashSet<string> DeactiveElemIds { get; set; }
        public HashSet<string> TrasportElemIds { get; private set; }
        public List<string> ObstructionElemIds { get; set; }

        public List<Document> RevitDocuments { get; set; }
        public List<Voxel> Voxel2Show { get; set; } = new List<Voxel>();

        
        public BackgroundWorker BackgroundWorker { get; set; }
        public bool OnlyExportMesh { get; set; } = false;
        public UIDocument UIDoc { get; set; }
        public FrmVoxel()
        {
            InitializeComponent();
            
        }
        public Vec3 origin { get; private set; } = Vec3.Zero;
        public double VoxSize { get; private set; }
        public ExternalEvent ShowPath { get; internal set; }

        private Stopwatch sw = new Stopwatch();

        private ExternalEvent showVoxelEvent;
        private DataTable dtModel;
        private void FrmVoxel_Load(object sender, EventArgs e)
        {
            chkDebug.Checked = false;
            btnSave.Enabled = false;
            chkDebug_CheckedChanged(chkDebug, null);
            dtModel = new DataTable();
            dtModel.Columns.Add("ElementId");
            dtModel.Columns.Add("Category");
            dtModel.Columns.Add("Name");
            dtModel.Columns.Add("Number of Triangles");
            dtModel.Columns.Add("Number of Lego Voxels");
            var dcTime=  dtModel.Columns.Add("Voxelzation Time");
            dcTime.DataType = typeof(double);
            
            
            //find room info
            var doc = this.RevitDocuments[0];
            FilteredElementCollector coll = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rooms);
            //update tws
        }
        
        public bool RemovePath { get; internal set; } = false;
        private int NumTriangle { get; set; } = 0;
        private int NumVoxels { get; set; } = 0;
        public ExternalEvent ConvertModel { get; internal set; }
        public ExternalEvent ShowCompressedVoxel { get; internal set; }
        public ExternalEvent ShowAccessibleRectangle { get; internal set; }
        public ExternalEvent ShowAccessibleRegion { get; internal set; }
        public bool OutputElementBySolid { get; internal set; } = false;

        private void BackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if(BackgroundWorker.CancellationPending==false)
            {
                foreach (var info in stringRows)
                {
                    dtModel.Rows.Add(info);
                }
                dgvResult.DataSource = dtModel;
                //if(allWorkDone)
                {
                    sw.Stop();
                    MessageBox.Show($"Voxelization done！time elapsed:{sw.Elapsed.TotalSeconds}");
                    //this.DialogResult = DialogResult.OK;
                    btnSave.Enabled = true;
                    ShowTriangleHandler.Mesh2Show = this.elem2Visulaize;
                    if(this.ShowTriangles)
                    {
                        this.ShowTriangle.Raise();
                    }
                }
                BackgroundWorker.Dispose();
            }
        }
        public void StartTiming()
        {
            this.sw.Start();
        }
        private void BackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            prog.Value += 1;
            this.Text = String.Format("{0}个已经完成，共{1}个", prog.Value, prog.Maximum);
            var info = e.UserState;
            if(info is MeshElement)
            {
                var meshlElem = e.UserState as MeshElement;
               
            }
            else
            {
                string[] strInfo = e.UserState as string[];
                if(strInfo != null)
                {
                    //dtModel.Rows.Add(strInfo);
                }
            }
            
            
            //throw new NotImplementedException();
        }
        public VoxelDocument VoxDoc = null;
        private List<MeshElement> elem2Visulaize;
        private bool ShowTriangles = false;
        private bool mergeVoxels = false;
        ConcurrentBag<string[]> stringRows = new ConcurrentBag<string[]>();
        private void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            stringRows = new ConcurrentBag<string[]>();
            this.sw.Restart();
            Stopwatch swTemp = new Stopwatch();
            mergeVoxels =!( this.chkDebug.Checked);
            try
            {
                NumTriangle = 0;
                NumVoxels = 0;
                elem2Visulaize = new List<MeshElement>();
                TempFileSaver meshSaver = e.Argument as TempFileSaver;
                TempFileSaver voxSaver = new TempFileSaver(this.VoxDoc, int.MaxValue / 2);
                //try save the meshSaver
                if (this.ShowTriangles) //the triangle will be visualized after voxelization
                {
                    foreach (var c in meshSaver.ReadMeshElementFromTempFile())
                    {
                        elem2Visulaize.Add(c);
                        NumTriangle += c.GetTriangleNumber();
                        BackgroundWorker.ReportProgress(0, c);
                    }
                }
                else
                {
                    foreach (var c in meshSaver.ReadMeshElementFromTempFile())
                    {
                        swTemp=Stopwatch.StartNew();
                        var ve = new VoxelElement(this.VoxDoc, c,mergeVoxels);
                        swTemp.Stop();
                        var trisNumInMe= c.GetTriangleNumber();
                        NumTriangle += trisNumInMe;
                        voxSaver.WriteVoxelElement(ve);
                        NumVoxels += ve.Voxels.Count;
                        string[] strInfo = new string[6] { c.ElementId, c.Category, c.Name, trisNumInMe.ToString(), ve.Voxels.Count.ToString(), swTemp.Elapsed.TotalMilliseconds.ToString("0") };
                        dtModel.Rows.Add(strInfo);
                        BackgroundWorker.ReportProgress(0, strInfo);
                    }
                }
                voxSaver.Finish();
                this.VoxDoc = voxSaver.ReadVoxelsFromTempFiles();
                /*
                else
                {
                    Stopwatch swAll = new Stopwatch();
                    ConcurrentBag<string> strTskInfos = new ConcurrentBag<string>();
                    ConcurrentBag<string[]> strRowInfo=new ConcurrentBag<string[]>();
                    ConcurrentBag<VoxelElement> elemDone = new ConcurrentBag<VoxelElement>();
                    var elemGp= meshSaver.ReadMeshElementsFromTempFile(2).ToList ();
                    swAll.Restart();
                    
                    Thread tsk1 = new Thread(() =>
                    {
                        Stopwatch sw0 = Stopwatch.StartNew();
                        int threadId= Environment.CurrentManagedThreadId;
                        foreach (var c in elemGp[0].Elements)
                        {
                            swTemp = Stopwatch.StartNew();
                            var ve = new VoxelElement(this.VoxDoc, c, mergeVoxels);
                            elemDone.Add(ve);
                            swTemp.Stop();
                            var trisNumInMe = c.GetTriangleNumber();
                            string[] strInfo = new string[6] { c.ElementId, c.Category, c.Name, trisNumInMe.ToString(), ve.Voxels.Count.ToString(), swTemp.Elapsed.TotalMilliseconds.ToString("0") };
                            strRowInfo.Add(strInfo);
                        }
                        sw0.Stop();
                        strTskInfos.Add($"ThreadId:{threadId},time:{sw0.Elapsed.TotalMilliseconds}");
                    });
                    tsk1.Start();
                    Thread tsk2 = new Thread(() =>
                    {
                        Stopwatch sw0 = Stopwatch.StartNew();
                        int threadId = Environment.CurrentManagedThreadId;
                        foreach (var c in elemGp[1].Elements)
                        {
                            swTemp = Stopwatch.StartNew();
                            var ve = new VoxelElement(this.VoxDoc, c, mergeVoxels);
                            elemDone.Add(ve);
                            var trisNumInMe = c.GetTriangleNumber();
                            swTemp.Stop();
                            string[] strInfo = new string[6] { c.ElementId, c.Category, c.Name, trisNumInMe.ToString(), ve.Voxels.Count.ToString(), swTemp.Elapsed.TotalMilliseconds.ToString("0") };
                            strRowInfo.Add(strInfo);
                        }
                        sw0.Stop();
                        strTskInfos.Add($"ThreadId:{threadId},time:{sw0.Elapsed.TotalMilliseconds}");
                    });
                    tsk2.Start();
                    tsk1.Join();
                    tsk2.Join();
                    string strResult=string.Join("\r\n",strTskInfos.ToArray());
                   
                    swAll.Stop();
                    MessageBox.Show(strResult+$"\r\n Time elapsed:{swAll.Elapsed.TotalSeconds}");
                }
                */
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace);
                e.Cancel = true;
            }
        }

        private void BackgroundWorker_DoWork_TestMode(object sender, DoWorkEventArgs e)
        {
            this.sw.Restart();
            mergeVoxels = !(this.chkDebug.Checked);
            try
            {
                TempFileSaver meshSaver = e.Argument as TempFileSaver;
                TempFileSaver voxElemSaver = new TempFileSaver(this.VoxDoc, int.MaxValue / 2);
                //Parallel.ForEach(meshSaver.ReadMeshElementFromTempFile(), c =>
                this.VoxDoc = new VoxelDocument();
                this.VoxDoc.Origin = this.origin;
                this.VoxDoc.VoxelSize = this.VoxSize;
                this.VoxDoc.Elements = new List<VoxelElement>();
                foreach (var c in meshSaver.ReadMeshElementFromTempFile())
                {
                    var ve = new VoxelElement(this.VoxDoc, c, mergeVoxels);
                    this.VoxDoc.Elements.Add(ve);
                    //voxElemSaver.WriteVoxelElement(ve);
                    BackgroundWorker.ReportProgress(0, c);
                }//);
                //voxElemSaver.Finish();
                //this.VoxDoc = voxElemSaver.ReadVoxelsFromTempFiles();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace);
                e.Cancel = true;
            }
        }
        private void btnGenerate_Click(object sender, EventArgs e)
        {
            this.ShowTriangles = chkShowTriangle.Checked;
            this.origin = Vec3.Zero;
            this.VoxSize = double.Parse(txtVoxSize.Text) / 304.8;
            btnSave.Enabled = false;
            this.VoxDoc = new VoxelDocument();
            this.VoxDoc.Elements = new List<VoxelElement>();
            VoxDoc.Origin = this.origin;
            VoxDoc.VoxelSize = this.VoxSize;
            //find support and obstruct elements
            this.SupportElemIds = new HashSet<string>();
            this.DeactiveElemIds = new HashSet<string>();
            this.TrasportElemIds = new HashSet<string>();
            for (int i = 0; i <= this.RevitDocuments.Count - 1; i++)
            {
                var doc = this.RevitDocuments[i];
                IList<ElementFilter> supElemFilter=new List<ElementFilter>();
                ElementCategoryFilter floorFilter = new ElementCategoryFilter(BuiltInCategory.OST_Floors);
                supElemFilter.Add(floorFilter);
                ElementCategoryFilter rampFilters = new ElementCategoryFilter(BuiltInCategory.OST_Ramps);
                supElemFilter.Add(rampFilters);
                ElementCategoryFilter stairFilters = new ElementCategoryFilter(BuiltInCategory.OST_Stairs);
                supElemFilter.Add(stairFilters);
                ElementCategoryFilter starRunFilter = new ElementCategoryFilter(BuiltInCategory.OST_StairsRuns);
                supElemFilter.Add(starRunFilter);
                ElementCategoryFilter columnFilter = new ElementCategoryFilter(BuiltInCategory.OST_StructuralColumns);
                supElemFilter.Add(columnFilter);
                ElementStructuralTypeFilter beamFilter = new ElementStructuralTypeFilter(Autodesk.Revit.DB.Structure.StructuralType.Beam);
                supElemFilter.Add (beamFilter);
#if !(Revit2016)
                ElementCategoryFilter stairCaseFilter = new ElementCategoryFilter(BuiltInCategory.OST_MultistoryStairs);
                supElemFilter.Add(stairCaseFilter);
#endif
                ElementCategoryFilter wallFilter = new ElementCategoryFilter(BuiltInCategory.OST_Walls);
                supElemFilter.Add(wallFilter);
                ElementCategoryFilter roofFilter = new ElementCategoryFilter(BuiltInCategory.OST_Roofs);
                supElemFilter.Add(roofFilter);
                var supAllElemFilter = new LogicalOrFilter(supElemFilter);
                FilteredElementCollector coll = new FilteredElementCollector(doc).WherePasses(supAllElemFilter);
                foreach (var elem in coll)
                {
                    string strElemId = i.ToString() + "$" + elem.Id.IntegerValue.ToString();
                    this.SupportElemIds.Add(strElemId);
                }
                //deactivage
                foreach (var door in new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Doors))
                {
                    string strElemId = i.ToString() + "$" + door.Id.IntegerValue.ToString();
                    DeactiveElemIds.Add(strElemId);
                }
                //transport
                var elems = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Doors);
                
                foreach (var elem in elems)
                {
                    var paramComment = elem.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                    if (paramComment!=null &&  paramComment.AsString()=="电梯门")
                    {
                        string strElemId = i.ToString() + "$" + elem.Id.IntegerValue.ToString();
                        this.TrasportElemIds.Add(strElemId);
                    }
                }
            }
           
            dtModel.Rows.Clear();
            this.GenerateVoxels.Raise();
        }
        public void InitProgress(int progMin, int progMax)
        {
            prog.Minimum = progMin;
            prog.Maximum = progMax;
            prog.Value = progMin;
        }

        public void UpdateProgress(string info)
        {
            prog.Value += 1;
            this.Text = info;
        }
        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                SaveFileDialog sfg = new SaveFileDialog();
                sfg.Filter = "voxel file|*.vox";
                if (DialogResult.OK == sfg.ShowDialog())
                {
                    VoxelDocumentConverter.SaveVoxelDocument(this.VoxDoc, sfg.FileName);
                }
                MessageBox.Show("文件写入完成");
                Process.Start("Explorer", "/select," + sfg.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace);
            }
        }
        private void btnLoad_Click(object sender, EventArgs e)
        {
            try
            {
                OpenFileDialog ofd = new OpenFileDialog();
                ofd.Multiselect = true;
                ofd.Filter = "voxel files|*.vox";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    string[] strFileName = ofd.FileNames;
                   
                    this.VoxDoc = VoxelDocumentConverter.LoadVoxelDocuments(ofd.FileNames);
                }
                MessageBox.Show("读取成功");
               
                this.btnSave.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace);
            }
        }

        private void btnVisualize_Click(object sender, EventArgs e)
        {
            this.showVoxelEvent.Raise();
        }
        private void FrmVoxel_FormClosed(object sender, FormClosedEventArgs e)
        {
            this.Dispose();
        }
        private Stopwatch swTest;
        private DataTable dtResult;
        private void btnConvert_Click(object sender, EventArgs e)
        {
            dtResult = new DataTable();
            dtResult.Columns.Add("ElementID");
            dtResult.Columns.Add("Number of Lego Voxels");
            dtResult.Columns.Add("Number of Test Voxels");
            dtResult.Columns.Add("Number of Missing Voxels");
            dtResult.Columns.Add("Number of Redundant Voxels");
            dtResult.Columns.Add("Missing Ratio");
            dtResult.Columns.Add("Redundancy Ratio");
            dgvResult.DataSource = dtResult;
            double dblSmallVoxSize = double.Parse(this.txtSmallVoxH.Text);
            dblSmallVoxSize /= 304.8;
            if(this.VoxDoc==null || this.VoxDoc.Elements.Count ==0)
            {
                MessageBox.Show("The voxelization has not been done,please voxel the model first");
            }
            BackgroundWorker bw2=new BackgroundWorker();
            bw2.WorkerReportsProgress = true;
            bw2.WorkerSupportsCancellation = true;
            bw2.DoWork += Bw2_DoWork;
            bw2.RunWorkerCompleted += Bw2_RunWorkerCompleted;
            bw2.ProgressChanged += Bw2_ProgressChanged;
            InitProgress(0, this.VoxDoc.Elements.Count);
            swTest = new Stopwatch();
            swTest.Start();
            view = this.RevitDocuments[0].ActiveView;
            bw2.RunWorkerAsync();
        }

        private void Bw2_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            prog.Value += 1;
            string[] strInfo = e.UserState as string[];
            this.Text = String.Format("{0} done, total {1}", prog.Value, prog.Maximum);
            if (strInfo[1]!=String.Empty)
            {
                string[] strRowData = strInfo[1].Split(',');
                //dtResult.Rows.Add(strRowData);
            }
            //throw new NotImplementedException();
        }

        private void Bw2_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            swTest.Stop();
            MessageBox.Show("Test complete!");
            ShowResultHandler.Origin = XYZ.Zero;
            ShowResultHandler.VoxelSize = this.VoxDoc.VoxelSize;
            ShowResultHandler.voxelVerticalSize = double.Parse(this.txtSmallVoxH.Text)/304.8;
            ShowResultHandler.VoxRedundancy = this.dicVoxOver;
            ShowResultHandler.VoxMissing = this.dicVoxMissing;
            ShowResultHandler.BoxSolids = this.dicBox;
            ShowResultHandler.ShowBox = this.chkShowBaseVoxel.Checked;
            ShowResultHandler.MeshElems=this.elemId_ME;
            this.ShowValidationResult.Raise();
            
            //throw new NotImplementedException();
        }
        private Autodesk.Revit.DB.View view;
        Dictionary<string, List<CellIndex3D>> dicVoxMissing = new Dictionary<string, List<CellIndex3D>>();
        Dictionary<string, List<CellIndex3D>> dicVoxOver = new Dictionary<string, List<CellIndex3D>>();
        Dictionary<string, List<Solid>> dicBox = new Dictionary<string, List<Solid>>();
        private void Bw2_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                dicVoxMissing.Clear();
                dicVoxOver.Clear();
                var wk = sender as BackgroundWorker;
                double dblVInteval = double.Parse(txtSmallVoxH.Text) / 304.8;
                foreach (var elem in this.VoxDoc.Elements)
                {
                    string docIdx = elem.ElementId.Split('$')[0];
                    string elemID = elem.ElementId.Split('$')[1];
                    var revitDoc = this.RevitDocuments[int.Parse(docIdx)];
                    var revidElem = revitDoc.GetElement(new ElementId(int.Parse(elemID)));
                    string[] strResult = new string[2];
                    strResult[0]= string.Format("ElementId:{0},Name{1}\r\n", elem.ElementId, elem.Name);
                    strResult[1] = string.Empty;
                    HashSet<CellIndex3D> occupiedBoxes = new HashSet<CellIndex3D>();
                    if (revidElem.GetType()==typeof(DirectShape))
                    {
                        var bbox = revidElem.get_BoundingBox(view);
                        if (bbox != null)
                        {
                            XYZ[] elemBox = new XYZ[2] { bbox.Min, bbox.Max };
                            if (elemBox != null)
                            {
                                if (!dicBox.ContainsKey(elemID))
                                {
                                    dicBox.Add(elemID, new List<Solid>() );
                                }
                                dicVoxMissing.Add(elem.ElementId, new List<CellIndex3D>());
                                dicVoxOver.Add(elem.ElementId, new List<CellIndex3D>());
                                //the box is coded as :colIndex_RowIndex_LayerIndex
                                XYZ boxMin = elemBox[0];
                                XYZ boxMax = elemBox[1];
                                //search solid 
                                int numVoxMissing = 0;
                                int numVoxOver = 0;

                                foreach (var box in GenerateVoxelsInBox(boxMin, boxMax, XYZ.Zero, VoxDoc.VoxelSize, dblVInteval))
                                {
                                    CellIndex3D boxIndex = new CellIndex3D(box.ColIndex, box.RowIndex, box.Layer);
                                    //occupiedBoxes.Add(boxIndex);
                                    var sld = box.GetSolid();
                                    ElementIntersectsSolidFilter filter = new ElementIntersectsSolidFilter(sld);
                                    //BoundingBoxIntersectsFilter filter = new BoundingBoxIntersectsFilter(new Outline(boxMin, boxMax));
                                    FilteredElementCollector coll = new FilteredElementCollector(revitDoc, new List<ElementId>() { revidElem.Id });
                                    if (coll.WherePasses(filter).Count() != 0)
                                    {
                                        occupiedBoxes.Add(boxIndex);
                                        if (this.chkShowBaseVoxel.Checked)
                                        {
                                            dicBox[elemID].Add(sld);
                                        }

                                    }
                                }
                                //restore current voxels
                                HashSet<CellIndex3D> cixVoxes = new HashSet<CellIndex3D>();
                                foreach (var vox in elem.Voxels)
                                {
                                    foreach (var voxCix in Voxel2Cell(vox, this.VoxDoc.Origin, dblVInteval))
                                    {
                                        //CellIndex3D cix = new CellIndex(vox.ColIndex, vox.RowIndex, vox.la);
                                        cixVoxes.Add(voxCix);
                                    }
                                }
                                //find missing voxel
                                foreach (var cixBox in occupiedBoxes)
                                {
                                    if (!cixVoxes.Contains(cixBox)) //a voxel is missing
                                    {
                                        dicVoxMissing[elem.ElementId].Add(cixBox);
                                        numVoxMissing += 1;
                                    }
                                }
                                //find over-voxelization voxel
                                foreach (var cixVox in cixVoxes)
                                {
                                    if (!occupiedBoxes.Contains(cixVox))
                                    {
                                        dicVoxOver[elem.ElementId].Add(cixVox);
                                        numVoxOver += 1;
                                    }
                                }
                                double dblRatioMissing = (double)numVoxMissing / occupiedBoxes.Count * 100;
                                double dblRatioOver = (double)numVoxOver / occupiedBoxes.Count * 100;
                                strResult[0] +=string.Format("  number of missing voxels:{0},Ratio{1}%,\r\n  number of redundant voxels{2},ratio{3}%\r\n",
                                                        numVoxMissing, dblRatioMissing.ToString("0.00"), numVoxOver, dblRatioOver.ToString("0.00"));
                                strResult[1] = string.Join(",", revidElem.Id, elem.Voxels.Count, occupiedBoxes.Count, numVoxMissing, numVoxOver, dblRatioMissing.ToString ("0.00"), dblRatioOver.ToString ("0.00"));


                            }
                            
                        }
                    }
                    string[] strRowData = strResult[1].Split(',');
                    dtResult.Rows.Add(strRowData);
                    wk.ReportProgress(0, strResult);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message+ex.StackTrace);
            }
            //throw new NotImplementedException();
        }
        private Solid Box2Solid(XYZ min,XYZ max)
        {
            var scale = max - min;
            XYZ p0 = min;
            XYZ p1 = min + XYZ.BasisX * scale.X;
            XYZ p2=p1+XYZ.BasisY * scale.Y;
            XYZ p3 = p0 + XYZ.BasisY * scale.Y;
            CurveLoop proflie = new CurveLoop();
            List<CurveLoop> loops=new List<CurveLoop>();
            loops.Add(proflie);
            var pts = new XYZ[4] { p0, p1, p2, p3 };
            for(int i=0;i<=3;i++)
            {
                Line li = Line.CreateBound(pts[i % 4], pts[(i + 1) % 4]);
                proflie.Append(li);
            }
            Solid sld = GeometryCreationUtilities.CreateExtrusionGeometry(loops, XYZ.BasisZ, scale.Z);
            return sld;
        }
        private XYZ[] GetElementBox(Autodesk.Revit.DB.View view,Element elem)
        {
            XYZ[] boxes = null;
            var opt = new Options();
            opt.View = view;
            var geoElem = elem.get_Geometry(opt);
            if(geoElem == null)
            {
                return null;
            }
            double xMin = double.MaxValue;
            double yMin = double.MaxValue;
            double zMin = double.MaxValue;
            double xMax = double.MinValue;
            double yMax = double.MinValue;
            double zMax = double.MinValue;
            bool boxExist = false;
            foreach (var geoObj in geoElem)
            {
                if (geoObj is Solid)
                {
                    var sld = geoObj as Solid;
                    if (sld.Faces.Size > 0)
                    {
                        boxExist = true;
                        foreach (Face fa in sld.Faces)
                        {
                            Mesh mesh = fa.Triangulate();
                            foreach (var v in mesh.Vertices)
                            {
                                xMin = Math.Min(v.X, xMin);
                                yMin =Math.Min (v.Y ,yMin);
                                zMin = Math.Min(v.Z, zMin);
                                xMax = Math.Max(v.X, xMax);
                                yMax = Math.Max(v.Y, yMax);
                                zMax = Math.Max(v.Z, zMax);
                            }
                        }
                    }
                }
                else if (geoObj is GeometryInstance)
                {
                    Stack<GeometryInstance> instance = new Stack<GeometryInstance>();
                    instance.Push((geoObj as GeometryInstance));
                    while (instance.Count > 0)
                    {
                        GeometryInstance geoIns = instance.Pop();
                        var geoInsElem = geoIns.GetInstanceGeometry();
                        if (geoInsElem != null)
                        {
                            foreach (var insGeoObj in geoInsElem)
                            {
                                if (insGeoObj is Solid)
                                {
                                    var sld = insGeoObj as Solid;
                                    if (sld.Faces.Size > 0)
                                    {
                                        boxExist = true;
                                        foreach (Face fa in sld.Faces)
                                        {
                                            Mesh mesh = fa.Triangulate();
                                            foreach (var v in mesh.Vertices)
                                            {
                                                xMin = Math.Min(v.X, xMin);
                                                yMin = Math.Min(v.Y, yMin);
                                                zMin = Math.Min(v.Z, zMin);
                                                xMax = Math.Max(v.X, xMax);
                                                yMax = Math.Max(v.Y, yMax);
                                                zMax = Math.Max(v.Z, zMax);
                                            }
                                        }
                                    }
                                }
                                else if (insGeoObj is GeometryInstance)
                                {
                                    instance.Push((GeometryInstance)insGeoObj);
                                }
                            }
                        }
                    }
                    
                }
            }
            if(boxExist)
            {
                return new XYZ[2] { new XYZ(xMin, yMin, zMin), new XYZ(xMax, yMax, zMax) };
                
            }
            else
            {
                return null;
            }
        }
        private CellIndex3D BoxToCell(Box box)
        {
            return new CellIndex3D(box.ColIndex,box.RowIndex,box.Layer);
        }

        private IEnumerable<CellIndex3D> Voxel2Cell(Voxel vox,Vec3 origin,double voxelHeight)
        {
            var voxBtm = vox.BottomElevation;
            var voxTop = vox.TopElevation;
            int layerSt = (int)Math.Floor(Math.Round((voxBtm - origin.Z) / voxelHeight, 4));
            int layerEd = (int)Math.Ceiling(Math.Round((voxTop - origin.Z) / voxelHeight, 4)) - 1;
            for (int layer = layerSt; layer <= layerEd; layer++)
            {
                yield return new CellIndex3D (vox.ColIndex ,vox.RowIndex,layer);
            }
        }
        private IEnumerable<Box> GenerateVoxelsInBox(XYZ boxMin,XYZ boxMax,  XYZ origin,double voxelSizeH,double voxelSizeV)
        {
            int colSt = (int) Math.Floor(Math.Round((boxMin - origin).X / voxelSizeH, 4));
            int colEd= (int)Math.Ceiling(Math.Round((boxMax- origin).X / voxelSizeH, 4))-1;
            int rowSt = (int)Math.Floor(Math.Round((boxMin - origin).Y / voxelSizeH, 4));
            int rowEd = (int)Math.Ceiling(Math.Round((boxMax - origin).Y / voxelSizeH, 4)) - 1;
            int layerSt=(int)Math.Floor(Math.Round((boxMin - origin).Z / voxelSizeV, 4));
            int layerEd = (int)Math.Ceiling(Math.Round((boxMax - origin).Z / voxelSizeV, 4)) - 1;
            for(int col=colSt; col<=colEd;col++)
            {
                for(int row=rowSt;row<=rowEd;row++)
                {
                    for(int layer=layerSt;layer<=layerEd;layer++)
                    {
                        var minX = origin.X + col * voxelSizeH;
                        var minY = origin.Y + row * voxelSizeH;
                        var minZ= origin.Z + layer * voxelSizeV;
                        var maxX = minX + voxelSizeH;
                        var maxY = minY+voxelSizeH;
                        var maxZ = minZ+voxelSizeV;
                        Box littleBox = new Box() { Min = new XYZ(minX, minY, minZ), Max = new XYZ(maxX, maxY, maxZ) ,ColIndex =col,RowIndex =row,Layer =layer};
                        yield return littleBox;
                    }
                }
            }
        }

        private void btnTestVoxOutput_Click(object sender, EventArgs e)
        {
            
            

        }

        private void chkDebug_CheckedChanged(object sender, EventArgs e)
        {
            var chk = sender as CheckBox;
            BackgroundWorker = new BackgroundWorker();
            BackgroundWorker.ProgressChanged += BackgroundWorker_ProgressChanged;
            BackgroundWorker.WorkerReportsProgress = true;
            BackgroundWorker.WorkerSupportsCancellation = true;
            BackgroundWorker.RunWorkerCompleted += BackgroundWorker_RunWorkerCompleted;
            if (chk.Checked==true)
            {
                this.BackgroundWorker.DoWork += BackgroundWorker_DoWork_TestMode;
                this.showVoxelEvent = this.VisualiezSingleVoxels;
            }
            else
            {
                this.BackgroundWorker.DoWork += BackgroundWorker_DoWork;
                this.showVoxelEvent = this.VisualiezVoxels;
            }
        }
        Dictionary<int, Voxel> dicIndex_Voxel;
       

        private void txtExportSolid_Click(object sender, EventArgs e)
        {
            txtExportSolid.Enabled = false;
            this.OutputElementBySolid=chkConvertSldByElems.Checked;
            this.ConvertModel.Raise();
        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            DataTable dtModelInfo = dtModel;
            DataTable dtTestResult=dgvResult.DataSource as DataTable;
            List<DataTable> dt2Export = new List<DataTable>() { dtModelInfo, dtTestResult };
            string modelInfoName= this.RevitDocuments[0].Title + "_ModelInfo.csv";
            string testResultName = this.RevitDocuments[0].Title + "_TestResult.csv";
            string[] strFileName = new string[2] { modelInfoName,testResultName };

            //if(dtResult !=null && dtResult.Rows.Count !=0)
            {
                FolderBrowserDialog fbg = new FolderBrowserDialog();
                fbg.Description = "Please select a folder to save the voxel info";
                if(DialogResult.OK == fbg.ShowDialog() && fbg.SelectedPath!="")
                {
                    if(!Directory.Exists(fbg.SelectedPath))
                    {
                        Directory.CreateDirectory(fbg.SelectedPath);
                    }
                    int p = 0;
                    foreach (var dt in dt2Export)
                    {
                        if(dt!=null && dt.Rows.Count>0)
                        {
                            string fileName = fbg.SelectedPath + "/" + strFileName[p];
                            StreamWriter sw = new StreamWriter(fileName, false, Encoding.Default);
                            string[] strItem = new string[dt.Columns.Count];
                            int i = 0;
                            foreach (DataColumn dc in dt.Columns)
                            {
                                strItem[i] = dc.ColumnName;
                                i += 1;
                            }
                            sw.WriteLine(string.Join(",", strItem));
                            foreach (DataRow dr in dt.Rows)
                            {
                                sw.WriteLine(string.Join(",", dr.ItemArray));
                            }
                            sw.Flush();
                            sw.Close();
                        }
                        p += 1;
                    }
                    MessageBox.Show("Test Result Saved Successfully");
                    Process.Start("Explorer", "/select," + fbg.SelectedPath);
                }
            }
        }
        BackgroundWorker bwValidation = new BackgroundWorker();
        private void btnVoxGenValidation_Click(object sender, EventArgs e)
        {
            this.origin = Vec3.Zero;
            this.VoxSize = double.Parse(txtVoxSize.Text) / 304.8;
            this.VoxDoc = new VoxelDocument();
            VoxDoc.Origin = this.origin;
            VoxDoc.VoxelSize = VoxSize;
            bwValidation = new BackgroundWorker();
            bwValidation.WorkerReportsProgress = true;
            bwValidation.WorkerSupportsCancellation = true;
            bwValidation.DoWork += BwValidation_DoWork;
            bwValidation.ProgressChanged += BackgroundWorker_ProgressChanged;
            bwValidation.RunWorkerCompleted += BackgroundWorker_RunWorkerCompleted;
            //get numElement
            this.view = this.RevitDocuments[0].ActiveView; 
            FilteredElementCollector coll = new FilteredElementCollector(this.RevitDocuments[0], view.Id).WhereElementIsNotElementType();
            int numElems = coll.Count();
            InitProgress(0, numElems);
            this.sw = new Stopwatch();
            sw.Start();
            bwValidation.RunWorkerAsync();
        }
        Dictionary<string, MeshElement> elemId_ME = new Dictionary<string, MeshElement>();
        private void BwValidation_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                Stopwatch swTemp = new Stopwatch();
                elemId_ME.Clear();
                mergeVoxels = true;
                Options opt = new Options();
                opt.View = view;
                var bw = sender as BackgroundWorker;
                FilteredElementCollector coll = new FilteredElementCollector(this.RevitDocuments[0], view.Id).WhereElementIsNotElementType();
                TempFileSaver voxElemSaver = new TempFileSaver(this.VoxDoc, int.MaxValue / 2);
                if (this.ShowTriangles) //the triangle will be visualized after voxelization
                {
                    foreach (var elem in coll)
                    {
                        MeshElement c = ToMeshElement(elem, opt);
                        elemId_ME.Add(c.ElementId, c);
                        elem2Visulaize.Add(c);
                        NumTriangle += c.GetTriangleNumber();
                        var ve = new VoxelElement(this.VoxDoc, c, mergeVoxels);
                        NumVoxels += ve.Voxels.Count;
                        voxElemSaver.WriteVoxelElement(ve);
                        bw.ReportProgress(0, c);
                    }
                }
                else
                {
                    foreach (var elem in coll)
                    {
                        MeshElement c = ToMeshElement(elem, opt);
                        elemId_ME.Add(c.ElementId, c);
                        swTemp.Restart();
                        var ve = new VoxelElement(this.VoxDoc, c, mergeVoxels);
                        sw.Stop();
                        var trisNumInMe= c.GetTriangleNumber();
                        NumTriangle += trisNumInMe;
                        voxElemSaver.WriteVoxelElement(ve);
                        NumVoxels += ve.Voxels.Count;
                        string[] strInfo = new string[6] { c.ElementId, c.Category, c.Name, trisNumInMe.ToString(), ve.Voxels.Count.ToString(), swTemp.Elapsed.TotalMilliseconds.ToString("0") };
                        bw.ReportProgress(0, strInfo);
                    }
                }
                voxElemSaver.Finish();
                this.VoxDoc = voxElemSaver.ReadVoxelsFromTempFiles();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace);
            }
            //throw new NotImplementedException();
        }
        private MeshElement ToMeshElement(Element elem,Options opt)
        {
            var result=new MeshElement();
            result.Name = elem.Name;
            result.Category = "Empty";
            if(elem.Category!=null)
            {
                result.Category = elem.Category.Name;
            }
            result.ElementId ="0$"+ elem.Id.ToString();
            result.Solids = new List<MeshSolid>();
            foreach (var solid in GetSolidInElement(elem,opt))
            {
                MeshSolid ms = ToMeshSolid(result, solid);
                result.Solids.Add(ms);
            }
            return result;
        }
        private MeshSolid ToMeshSolid(MeshElement me, Solid solid)
        {
           
            List<Vec3> vertices = new List<Vec3>();
            List<int> triangles = new List<int>();
            foreach (Face fa in solid.Faces)
            {
                int offset = vertices.Count;
                var mesh=  fa.Triangulate(1);
                if(mesh!=null)
                {
                    foreach (var v in mesh.Vertices)
                    {
                        vertices.Add(ToVec3(v));
                    }
                    for (int i = 0; i < mesh.NumTriangles; i++)
                    {
                        var tri = mesh.get_Triangle(i);
                        for (int j = 0; j <= 2; j++)
                        {
                            int vi = (int)tri.get_Index(j) + offset;
                            triangles.Add(vi);
                        }
                    }
                }
            }
            var result=new MeshSolid (me, vertices, triangles);
            return result;
        }
        private Vec3 ToVec3(XYZ pt)
        {
            return new Vec3(pt.X, pt.Y, pt.Z);
        }
        
        public IEnumerable<Solid> GetSolidInElement(Element elem, Options option)
        {
            
            GeometryElement geoElem = elem.get_Geometry(option);
            if(geoElem != null)
            {
                foreach (var geoObject in geoElem)
                {
                    if(geoObject is Solid)
                    {
                        var sld = geoObject as Solid;
                        if(sld.Faces .Size >0)
                        {
                            yield return sld;
                        }
                    }
                    else if(geoObject is GeometryInstance)
                    {
                        var geoIns=geoObject as GeometryInstance;
                        foreach (var sld in GetSolidInGeometryInstance(geoIns))
                        {
                            yield return sld;
                        }
                    }
                }
            }
        }

        public IEnumerable<Solid> GetSolidInGeometryInstance(GeometryInstance ins)
        {
            var geoElem = ins.GetInstanceGeometry();
            if (geoElem != null)
            {
                foreach (var geoObject in geoElem)
                {
                    if (geoObject is Solid)
                    {
                        var sld = geoObject as Solid;
                        if (sld.Faces.Size > 0)
                        {
                            yield return sld;
                        }
                    }
                    else if(geoObject is GeometryInstance)
                    {
                        var geoIns=geoObject as GeometryInstance;
                        foreach(var sld in GetSolidInGeometryInstance(geoIns))
                        {
                            yield return sld;
                        }
                    }
                }
            }
        }

        private void dgvResult_RowEnter(object sender, DataGridViewCellEventArgs e)
        {
           
        }

        private void dgvResult_RowHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {

            DataGridView dgv = sender as DataGridView;
            int rowSel = e.RowIndex;
            if (rowSel != -1)
            {
                DataGridViewRow dr = dgv.Rows[rowSel];
                var elemIdVal = dr.Cells[0].Value.ToString();
                var elemId = "0$" + elemIdVal;
                ShowResultHandler.ElemId2Visualize = elemId;
                ShowResultHandler.MeshElems = elemId_ME;
                this.ShowValidationResult.Raise();
            }
        }

        private void dgvResult_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }

        private void btnGenerateByMesh_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "mesh files|*.voxMesh";
            if(DialogResult.OK ==ofd.ShowDialog ())
            {
                string strFileName = ofd.FileName;
                try
                {
                    this.ShowTriangles = chkShowTriangle.Checked;
                    this.origin = Vec3.Zero;
                    this.VoxSize = double.Parse(txtVoxSize.Text) / 304.8;
                    btnSave.Enabled = false;
                    this.VoxDoc = new VoxelDocument();
                    this.VoxDoc.Elements = new List<VoxelElement>();
                    VoxDoc.Origin = this.origin;
                    VoxDoc.VoxelSize = this.VoxSize;
                    BackgroundWorker = new BackgroundWorker();
                    BackgroundWorker.WorkerReportsProgress = true;
                    BackgroundWorker.WorkerSupportsCancellation = true;
                    BackgroundWorker.DoWork += BackgroundWorker_DoWork;
                    BackgroundWorker.ProgressChanged += BackgroundWorker_ProgressChanged;
                    BackgroundWorker.RunWorkerCompleted += BackgroundWorker_RunWorkerCompleted;
                    TempFileSaver saver = new TempFileSaver();
                    saver.SetPath(strFileName, false);
                    int numElem = saver.GetApproximateElementNumber();
                    InitProgress(0, numElem);
                    
                    dtModel.Rows.Clear();
                    BackgroundWorker.RunWorkerAsync(saver);
                }
                catch(Exception ex)
                {
                    MessageBox.Show(ex.Message + ex.StackTrace);
                }
            }
        }

        private void btnCompressVoxes_Click(object sender, EventArgs e)
        {
            FrmCompressMesh frmComp = new FrmCompressMesh();
            frmComp.Show(this);
        }

        private void btnSaveCSV_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfg = new SaveFileDialog();
            sfg.Filter = "csv file|*.csv";
            if (DialogResult.OK == sfg.ShowDialog())
            {
                VoxelDocumentConverter.SaveAsCSV(this.VoxDoc, sfg.FileName);
            }
            MessageBox.Show("文件写入完成");
            Process.Start("Explorer", "/select," + sfg.FileName);
        }
    }
    public class Box
    {
        public int ColIndex { get; set; }
        public int RowIndex { get; set; }
        public int Layer { get; set; }
        public bool IsIntersect { get; set; } = false;
        public XYZ Min { get; set; }
        public XYZ Max { get; set; }
        public Solid GetSolid()
        {
            var scale = Max - Min;
            var sizeX = scale.X;
            var sizeY=scale.Y;
            var sizeZ = scale.Z;
           
            XYZ pt0 = Min;
            XYZ pt1 = pt0 + XYZ.BasisX * sizeX;
            XYZ pt2 = pt1 + XYZ.BasisY * sizeY;
            XYZ pt3 = pt2 - XYZ.BasisX * sizeX;
            XYZ[] pts = new XYZ[] { pt0, pt1, pt2, pt3 };
            CurveLoop loop = new CurveLoop();
            for(int i=0;i<=3;i++)
            {
                Line li = Line.CreateBound(pts[i], pts[(i + 1) % 4]);
                loop.Append(li);
            }
            List<CurveLoop> loops = new List<CurveLoop>() { loop };
            var sld = GeometryCreationUtilities.CreateExtrusionGeometry(loops, XYZ.BasisZ, sizeZ);
            return sld;
        }
    }

    
}
