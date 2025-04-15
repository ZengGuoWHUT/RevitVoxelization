using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.Creation;
using Document = Autodesk.Revit.DB.Document;
using System.Diagnostics;

using System.Windows.Forms;
using View = Autodesk.Revit.DB.View;
using System.IO;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;

namespace RevitVoxelzation
{
    [Transaction(TransactionMode.Manual)]
    public class LEGOVoxelApp : IExternalApplication
    {
        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
            //throw new NotImplementedException();
        }

        public Result OnStartup(UIControlledApplication application)
        {
            //add a tab
            application.CreateRibbonTab("GZ Tools");
            string strAssemblyPath=  Assembly.GetExecutingAssembly().Location;
            PushButtonData data = new PushButtonData("LEGOVox", "Voxelize", strAssemblyPath, "RevitVoxelzation.Revit2LEGO");
            RibbonPanel panel = application.CreateRibbonPanel("GZ Tools", "Voxelization");
            PushButton btn=panel.AddItem(data) as PushButton;
            btn.ToolTip = "Voxelize model";
            btn.LongDescription = "Voxelize model";
            return Result.Succeeded;
            //throw new NotImplementedException();
        }
    }
    [Transaction(TransactionMode.Manual)]
    public class Revit2LEGO : IExternalCommand
    {
        public static MeshDocument MeshDoc { get; set; }
        public static UIDocument UIDocument { get; set; }
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            MeshDoc = null;
            var uiapp = commandData.Application;
            var uidoc = uiapp.ActiveUIDocument;
            UIDocument = uidoc;
            var doc = uidoc.Document;
            var frm = new FrmVoxel();
            ExternalEvent voxelizeDocEvent = ExternalEvent.Create(new ExportMeshHandler(frm));
            ExternalEvent ShowVoxels=ExternalEvent.Create(new ShowVoxelHandler(frm));
            ExternalEvent ShowSingleVoxels = ExternalEvent.Create(new ShowSingleVoxelHandler(frm));
            ExternalEvent ShowValidationResult = ExternalEvent.Create(new ShowResultHandler());
            ExternalEvent ShowTriangle = ExternalEvent.Create(new ShowTriangleHandler());
            ExternalEvent ConverModel = ExternalEvent.Create(new ExportSolidInModel(frm));
            ExternalEvent ShowCompressedVoxel = ExternalEvent.Create(new LoadCompressedVoxelFiles());
            ExternalEvent ShowAccessibleRectangles = ExternalEvent.Create(new ShowRawAccessibleRegion());
            ExternalEvent ShowAccessibleRegions = ExternalEvent.Create(new ShowRegionHandler());
            ExternalEvent ShowPath = ExternalEvent.Create(new ShowPathHandler());

            frm.GenerateVoxels = voxelizeDocEvent;
            frm.VisualiezVoxels = ShowVoxels;
            frm.VisualiezSingleVoxels = ShowSingleVoxels;
            frm.ShowValidationResult = ShowValidationResult;
            frm.ShowTriangle = ShowTriangle;
            frm.ConvertModel = ConverModel;
            frm.ShowCompressedVoxel = ShowCompressedVoxel;
            frm.ShowAccessibleRectangle = ShowAccessibleRectangles;
            frm.ShowAccessibleRegion = ShowAccessibleRegions;
            frm.ShowPath = ShowPath;

            //create a exporter
            Dictionary<Document, int> dicDoc_Index = new Dictionary<Document, int>();
            dicDoc_Index.Add(doc, 0);
            foreach (Document linkDoc in uiapp.Application.Documents)
            {
                if (linkDoc.IsLinked)
                {
                    dicDoc_Index.Add(linkDoc, dicDoc_Index.Count);
                }
            }
            frm.RevitDocuments = dicDoc_Index.Keys.ToList();
            frm.UIDoc = uidoc;
            frm.Show();
            return Result.Succeeded;
            //throw new NotImplementedException();
        }
    }
    public class CountElementExporter : IExportContext
    {
        public int NumElement { get; set; } = 0;
        
        public void Finish()
        {
           
            //throw new NotImplementedException();
        }

        public bool IsCanceled()
        {
            return false;
            //throw new NotImplementedException();
        }

        public RenderNodeAction OnElementBegin(ElementId elementId)
        {
            NumElement += 1;
            return RenderNodeAction.Proceed;
            //throw new NotImplementedException();
        }

        public void OnElementEnd(ElementId elementId)
        {
            //throw new NotImplementedException();
        }

        public RenderNodeAction OnFaceBegin(FaceNode node)
        {
            return RenderNodeAction.Skip;
            //throw new NotImplementedException();
        }

        public void OnFaceEnd(FaceNode node)
        {
            //throw new NotImplementedException();
        }

        public RenderNodeAction OnInstanceBegin(InstanceNode node)
        {
            return RenderNodeAction.Skip;
            //throw new NotImplementedException();
        }

        public void OnInstanceEnd(InstanceNode node)
        {
            //throw new NotImplementedException();
        }

        public void OnLight(LightNode node)
        {
            //throw new NotImplementedException();
        }

        public RenderNodeAction OnLinkBegin(LinkNode node)
        {
            return RenderNodeAction.Proceed;
            //throw new NotImplementedException();
        }

        public void OnLinkEnd(LinkNode node)
        {
           // throw new NotImplementedException();
        }

        public void OnMaterial(MaterialNode node)
        {
            //throw new NotImplementedException();
        }

        public void OnPolymesh(PolymeshTopology node)
        {
            //throw new NotImplementedException();
        }

        public void OnRPC(RPCNode node)
        {
            //throw new NotImplementedException();
        }

        public RenderNodeAction OnViewBegin(ViewNode node)
        {
            return RenderNodeAction.Proceed;
            //throw new NotImplementedException();
        }

        public void OnViewEnd(ElementId elementId)
        {
            //throw new NotImplementedException();
        }

        public bool Start()
        {
            return true;
            //throw new NotImplementedException();
        }
#if Revit2016 || Revit2015
        public void OnDaylightPortal(DaylightPortalNode node)
        {
            
        }
            
#endif
    }

    public class VoxelizeElementExporter2 : IExportContext
    {
        public Dictionary<Document, int> Doc_Inxex { get; set; }
        private Stack<Document> docProccessing = new Stack<Document>();
        private Stack<MeshDocument> meshDocProcessing = new Stack<MeshDocument>();
        private Stack<MeshElement> elemProcessing = new Stack<MeshElement>();
        private Stack<List<MeshData>> stkMeshData = new Stack<List<MeshData>>();
        private Stack<Autodesk.Revit.DB.Transform> stkTransform = new Stack<Autodesk.Revit.DB.Transform>();
        public MeshDocument MainDoc { get; set; }
        public int NumElements { get; set; }
        private Stopwatch sw = new Stopwatch();

        public FrmVoxel Owner { get; set; }
        public void Finish()
        {
            try
            {
                MainDoc = meshDocProcessing.Pop();
                sw.Stop();
                this.Owner.InitProgress(0, this.NumElements);
                string timeElapsed_Voxelization = sw.Elapsed.TotalSeconds.ToString("0.000");
                //this.Owner.BackgroundWorker.RunWorkerAsync(saver);
                meshSaver.Finish();
                meshSaver.ElementCount = elemCountActual;
                if (MessageBox.Show("Do you need to save the mesh file?", "Caution", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    SaveFileDialog sfg = new SaveFileDialog();
                    sfg.Filter = "Mesh files|*.voxMesh";
                    if (DialogResult.OK == sfg.ShowDialog())
                    {
                        meshSaver.SaveAs(sfg.FileName,true);
                        meshSaver.DeleteAfterRead = false;
                    }
                }
                if(MessageBox.Show("Continue voxelization process?","Caution",MessageBoxButtons.YesNo)==DialogResult.Yes)
                {
                    this.Owner.BackgroundWorker.RunWorkerAsync(meshSaver);
                }
                else
                {
                    Process.Start("Explorer", "/select," + meshSaver.GetPath());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace);
            }
            //Owner.InitProgress(0, MainDoc.Elements.Count);
            //Owner.StartTiming();
            //Owner.BackgroundWorker.RunWorkerAsync();
        }
        public bool IsCanceled()
        {
            return error;
            //throw new NotImplementedException();
        }
#if Revit2016 || Revit2015
        public void OnDaylightPortal(DaylightPortalNode node)
        {
            
        }
            
#endif
        int elemCountActual = 0;
        public RenderNodeAction OnElementBegin(ElementId elementId)
        {
            //get element
            var doc = docProccessing.Peek();
            var elem = doc.GetElement(elementId);
            if (!(elem is RevitLinkInstance))
            {
                elemCountActual += 1;
                string strDocElementID = string.Format("{0}${1}", Doc_Inxex[docProccessing.Peek()], elementId.IntegerValue);
                stkMeshData.Push(new List<MeshData>());
                var me = new MeshElement(meshDocProcessing.Peek(), strDocElementID, new List<MeshSolid>()); ;
                if(Owner.SupportElemIds.Contains (strDocElementID))
                {
                    me.IsSupportElem = true;
                }
                if(Owner.DeactiveElemIds.Contains (strDocElementID))
                {
                    me.IsActive = false;
                }
                if(Owner.TrasportElemIds.Contains(strDocElementID))
                {
                    me.isTransport = true;
                }
                me.Category = elem.Category.Name;
                me.Name = elem.Name;
                elemProcessing.Push(me);
            }
            
            return RenderNodeAction.Proceed;
            // throw new NotImplementedException();
        }
        private bool error = false;
        public void OnElementEnd(ElementId elementId)
        {
            try
            {
                if (elemProcessing.Count != 0)
                {
                    var me = elemProcessing.Peek();
                    //Note: It is time-consuming for creating solid for mesh element
                    //Constider saving it as a temp file
                    //then processing it later
                    CreateSolidForMeshElement(me, stkMeshData.Peek());
                    meshSaver.WriteMeshElement(me,true);
                    //VoxelElement ve = new VoxelElement(this.Owner.VoxDoc, me);
                    //voxSaver.WriteVoxelElement(ve);
                    elemProcessing.Pop();
                    stkMeshData.Pop();
                }

            }
            catch (Exception ex)
            {
                TaskDialog.Show("Revit", ex.Message + ex.StackTrace);
                error = true;
            }

            //throw new NotImplementedException();
        }

        public RenderNodeAction OnFaceBegin(FaceNode node)
        {
            return RenderNodeAction.Proceed;
            //throw new NotImplementedException();
        }

        public void OnFaceEnd(FaceNode node)
        {
            //throw new NotImplementedException();
        }

        public RenderNodeAction OnInstanceBegin(InstanceNode node)
        {
            var totaltrf = stkTransform.Peek().Multiply(node.GetTransform());
            stkTransform.Push(totaltrf);
            return RenderNodeAction.Proceed;
            //throw new NotImplementedException();
        }

        public void OnInstanceEnd(InstanceNode node)
        {
            stkTransform.Pop();
            //throw new NotImplementedException();
        }
        public void OnLight(LightNode node)
        {
            //throw new NotImplementedException();
        }

        public RenderNodeAction OnLinkBegin(LinkNode node)
        {
            var totaltrf = stkTransform.Peek().Multiply(node.GetTransform());
            stkTransform.Push(totaltrf);
            var linkDoc = node.GetDocument();
            docProccessing.Push(linkDoc);
            return RenderNodeAction.Proceed;
            //throw new NotImplementedException();
        }

        public void OnLinkEnd(LinkNode node)
        {
            stkTransform.Pop();
            docProccessing.Pop();
            //throw new NotImplementedException();
        }

        public void OnMaterial(MaterialNode node)
        {

            //throw new NotImplementedException();
        }

        public void OnPolymesh(PolymeshTopology node)
        {
            try
            {
                var meshData = new MeshData(node, stkTransform.Peek());
                stkMeshData.Peek().Add(meshData);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Revit", ex.Message + ex.StackTrace);
            }
            //throw new NotImplementedException();
        }

        public void OnRPC(RPCNode node)
        {
            //throw new NotImplementedException();
        }

        public RenderNodeAction OnViewBegin(ViewNode node)
        {
            return RenderNodeAction.Proceed;
            //throw new NotImplementedException();
        }

        public void OnViewEnd(ElementId elementId)
        {
            //throw new NotImplementedException();
        }
        private TempFileSaver voxSaver;
        private TempFileSaver meshSaver;
        public bool Start()
        {
            docProccessing.Push(Doc_Inxex.Keys.FirstOrDefault());
            MeshDocument meshDoc = new MeshDocument(docProccessing.Peek().Title, new List<MeshElement>(), Transform.Idnentity);
            meshDocProcessing.Push(meshDoc);
            stkTransform.Push(Autodesk.Revit.DB.Transform.Identity);
            //voxSaver = new TempFileSaver(this.Owner.VoxDoc, int.MaxValue / 2,NumElements);
            meshSaver = new TempFileSaver(int.MaxValue / 2, NumElements);
            sw.Start();
            //this.Owner.BackgroundWorker.RunWorkerAsync(saver);
            //this.Owner.StartTiming();
            return true;
        }

        private RevitVoxelzation.Transform ConvertTransform(Autodesk.Revit.DB.Transform revitTransform)
        {
            Vec3 basisX = XYZ2Vec3(revitTransform.BasisX);
            Vec3 basisY = XYZ2Vec3(revitTransform.BasisY);
            Vec3 basisZ = XYZ2Vec3(revitTransform.BasisZ);
            Vec3 origin = XYZ2Vec3(revitTransform.Origin);
            return new Transform(basisX, basisY, basisZ, origin);

        }
        private Vec3 XYZ2Vec3(XYZ xyz)
        {
            return new Vec3(xyz.X, xyz.Y, xyz.Z);
        }
        public void CreateSolidForMeshElement(MeshElement me, List<MeshData> meshes)
        {
            //create meshSolid for me
            List<Vec3> verticesInSld = new List<Vec3>();
            List<int> TriVerticesIndexes = new List<int>();
            int offset = 0;
            for (int p = 0; p <= meshes.Count - 1; p++)
            {
                var mesh = meshes[p];
                var vertices = mesh.Vertices;
                foreach (var v in vertices)
                {
                    Vec3 vec3 = new Vec3(v.X, v.Y, v.Z);
                    verticesInSld.Add(vec3);
                }
                for (int i = 0; i <= mesh.Triangles.Count - 1; i += 3)
                //foreach (var tri in mesh.GetFacets())
                {
                    int vi0 = offset + mesh.Triangles[i];
                    int vi1 = offset + mesh.Triangles[i + 1];
                    int vi2 = offset + mesh.Triangles[i + 2];
                    TriVerticesIndexes.Add(vi0);
                    TriVerticesIndexes.Add(vi1);
                    TriVerticesIndexes.Add(vi2);
                }
                offset += mesh.Vertices.Count;
            }
            MeshSolid ms = new MeshSolid(me, verticesInSld, TriVerticesIndexes);
            me.Solids.Add(ms);
        }
    }

    public class MeshData
    {
        public List<XYZ> Vertices { get; set; }
        public List<int> Triangles { get; set; } = new List<int>();
        public MeshData(PolymeshTopology surfaceNode, Autodesk.Revit.DB.Transform trf)
        {
            this.Vertices = TransfromPoints(surfaceNode.GetPoints(), trf);
            foreach (var tri in surfaceNode.GetFacets())
            {
                Triangles.Add(tri.V1);
                Triangles.Add(tri.V2);
                Triangles.Add(tri.V3);
            }
        }

        private List<XYZ> TransfromPoints(IList<XYZ> points, Autodesk.Revit.DB.Transform trf)
        {
            List<XYZ> result = new List<XYZ>() { Capacity = points.Count };
            foreach (var pt in points)
            {
                result.Add(trf.OfPoint(pt));
            }
            return result;
        }
    }


    public class ExportMeshHandler : IExternalEventHandler
    {
        public FrmVoxel Owner { get; set; }
        public ExportMeshHandler(FrmVoxel owner)
        {
            Owner = owner;
        }

        public void Execute(UIApplication app)
        {
            try
            {
                var uidoc = app.ActiveUIDocument;
                var doc = uidoc.Document;
                //create a exporter
                Dictionary<Document, int> dicDoc_Index = new Dictionary<Document, int>();
                dicDoc_Index.Add(doc, 0);
                foreach (Document linkDoc in app.Application.Documents)
                {
                    if (linkDoc.IsLinked)
                    {
                        dicDoc_Index.Add(linkDoc, dicDoc_Index.Count);
                    }
                }
                var viewId = doc.ActiveView.Id;
                //find element count
                var elemCounter = new CountElementExporter() {};
                CustomExporter countExporter = new CustomExporter(doc, elemCounter);
                countExporter.Export(new List<ElementId>() { viewId });
                this.Owner.InitProgress(0, elemCounter.NumElement);
                var ve = new VoxelizeElementExporter2() { Doc_Inxex = dicDoc_Index, Owner = this.Owner ,NumElements=elemCounter.NumElement};
                CustomExporter exporter = new CustomExporter(doc, ve);
                exporter.ShouldStopOnError = true;
                exporter.Export(new List<ElementId>() { viewId });
                //System.Threading.Thread.Sleep(1000); 
                //this.Owner.BackgroundWorker.RunWorkerAsync();


            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace);
            }
        }

        

        public string GetName()
        {
            return "Export elements";
            //throw new NotImplementedException();
        }
    }

    public class ShowVoxelHandler : IExternalEventHandler
    {
        public FrmVoxel Owner { get; set; }
        public ShowVoxelHandler(FrmVoxel owner)
        {
            Owner = owner;
        }
        private List<ElementId> VoxelIds2Remove = new List<ElementId>();
        public void Execute(UIApplication app)
        {
            try
            {
                var doc = app.ActiveUIDocument.Document;
                using (Transaction t = new Transaction(doc))
                {
                    t.Start("Show voxels");
                    if (VoxelIds2Remove.Count != 0)
                    {
                        foreach (var voxId in VoxelIds2Remove)
                        {
                            if (doc.GetElement(voxId) != null)
                            {
                                doc.Delete(voxId);
                            }
                        }
                    }
                    VoxelIds2Remove.Clear();
                    //Generaete voxel
                    FillPatternElement solidFill = null;
                    FilteredElementCollector coll = new FilteredElementCollector(doc).OfClass(typeof(FillPatternElement));
                    foreach (FillPatternElement fpe in coll)
                    {
                        if (fpe.GetFillPattern().IsSolidFill)
                        {
                            solidFill = fpe;
                        }
                    }
                    var origin = Owner.VoxDoc.Origin;
                    var voxelSize = Owner.VoxDoc.VoxelSize;
                    if (Owner.Voxel2Show.Count == 0)
                    {
                        foreach (var ve in Owner.VoxDoc.Elements)
                        {
                            List<Solid> solids = new List<Solid>();
                            foreach (var v in ve.Voxels)
                            {
                                
                                //create voxel solid
                                double dblGPX = origin.X + v.ColIndex * voxelSize;
                                double dblGPY = origin.Y + v.RowIndex * voxelSize;
                                double dblGPZ = v.BottomElevation;
                                XYZ pt0 = new XYZ(dblGPX, dblGPY, dblGPZ);
                                XYZ pt1 = pt0 + new XYZ(voxelSize, 0, 0);
                                XYZ pt2 = pt1 + new XYZ(0, voxelSize, 0);
                                XYZ pt3 = pt2 - new XYZ(voxelSize, 0, 0);
                                Line l0 = Line.CreateBound(pt0, pt1);
                                Line l1 = Line.CreateBound(pt1, pt2);
                                Line l2 = Line.CreateBound(pt2, pt3);
                                Line l3 = Line.CreateBound(pt3, pt0);
                                CurveLoop crvLoop = new CurveLoop();
                                crvLoop.Append(l0);
                                crvLoop.Append(l1);
                                crvLoop.Append(l2);
                                crvLoop.Append(l3);
                                var crvLoops = new List<CurveLoop>() { crvLoop };
                                double dblExtrusionDist = Math.Max(1 / 304.8, v.TopElevation - v.BottomElevation);
                                Solid sld = GeometryCreationUtilities.CreateExtrusionGeometry(crvLoops, XYZ.BasisZ, dblExtrusionDist);
                                solids.Add(sld);
 

                            }
                            DirectShape dsGp = null;
#if Revit2016
                            dsGp = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel), new Guid().ToString(), new Guid().ToString());
#else
                            dsGp = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
#endif
                            dsGp.AppendShape(solids.ToArray());
                            dsGp.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(String.Format("Name:{0};Category:{1};Id:{2}", ve.Name, ve.Category, ve.ElementId));
                            //dsGp.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(String.Format("Index{0}, col:{1};row:{2};boundary:{3};BottomActivator:{4};TopActivator;{5};Boundary Activator{6},TopOutside:{7};BtmOutside:{8}",
                            //v.Index, v.ColIndex, v.RowIndex, v.IsBoundaryVoxel, v.BottomActivater, v.TopActivater, v.BoundaryActivater, v.TopOutside, v.BottomOutside));
                            MultiVerTool.ModifyOutfit(doc.ActiveView, dsGp.Id, solidFill.Id, new Color(0, 255, 0), 10);
                            VoxelIds2Remove.Add(dsGp.Id);

                        }
                    }
                    else
                    {
                        List<Solid> solids = new List<Solid>();
                        foreach (var v in Owner.Voxel2Show)
                        {
                            //create voxel solid
                            double dblGPX = origin.X + v.ColIndex * voxelSize;
                            double dblGPY = origin.Y + v.RowIndex * voxelSize;
                            double dblGPZ = v.BottomElevation;
                            XYZ pt0 = new XYZ(dblGPX, dblGPY, dblGPZ);
                            XYZ pt1 = pt0 + new XYZ(voxelSize, 0, 0);
                            XYZ pt2 = pt1 + new XYZ(0, voxelSize, 0);
                            XYZ pt3 = pt2 - new XYZ(voxelSize, 0, 0);
                            Line l0 = Line.CreateBound(pt0, pt1);
                            Line l1 = Line.CreateBound(pt1, pt2);
                            Line l2 = Line.CreateBound(pt2, pt3);
                            Line l3 = Line.CreateBound(pt3, pt0);
                            CurveLoop crvLoop = new CurveLoop();
                            crvLoop.Append(l0);
                            crvLoop.Append(l1);
                            crvLoop.Append(l2);
                            crvLoop.Append(l3);
                            var crvLoops = new List<CurveLoop>() { crvLoop };
                            double dblExtrusionDist = Math.Max(1 / 304.8, v.TopElevation - v.BottomElevation);
                            Solid sld = GeometryCreationUtilities.CreateExtrusionGeometry(crvLoops, XYZ.BasisZ, dblExtrusionDist);
                            solids.Add(sld);
                        }
                        DirectShape dsGp = null;
#if Revit2016
                        dsGp = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel), new Guid().ToString(), new Guid().ToString());
#else
                dsGp = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
#endif
                        dsGp.AppendShape(solids.ToArray());
                        MultiVerTool.ModifyOutfit(doc.ActiveView, dsGp.Id, solidFill.Id, new Color(0, 255, 0), 90);
                        VoxelIds2Remove.Add(dsGp.Id);
                    }
                    t.Commit();
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Revit", ex.Message + ex.StackTrace);
            }
        }

        public string GetName()
        {
            return "Visualize elements";
            //throw new NotImplementedException();
        }
    }

    public class ShowSingleVoxelHandler : IExternalEventHandler
    {
        public FrmVoxel Owner { get; set; }
        public ShowSingleVoxelHandler(FrmVoxel owner)
        {
            Owner = owner;
        }
        private List<ElementId> VoxelIds2Remove = new List<ElementId>();
        public void Execute(UIApplication app)
        {
            try
            {
                var doc = app.ActiveUIDocument.Document;
                using (Transaction t = new Transaction(doc))
                {
                    t.Start("Show voxels");
                    if (VoxelIds2Remove.Count != 0)
                    {
                        foreach (var voxId in VoxelIds2Remove)
                        {
                            if (doc.GetElement(voxId) != null)
                            {
                                doc.Delete(voxId);
                            }
                        }
                    }
                    VoxelIds2Remove.Clear();
                    //Generaete voxel
                    FillPatternElement solidFill = null;
                    FilteredElementCollector coll = new FilteredElementCollector(doc).OfClass(typeof(FillPatternElement));
                    foreach (FillPatternElement fpe in coll)
                    {
                        if (fpe.GetFillPattern().IsSolidFill)
                        {
                            solidFill = fpe;
                        }
                    }
                    var origin = Owner.VoxDoc.Origin;
                    var voxelSize = Owner.VoxDoc.VoxelSize;
                    if (Owner.Voxel2Show.Count == 0)
                    {
                        foreach (var ve in Owner.VoxDoc.Elements)
                        {
                            foreach (var v in ve.Voxels)
                            {
                                List<Solid> solids = new List<Solid>();
                                //create voxel solid
                                double dblGPX = origin.X + v.ColIndex * voxelSize;
                                double dblGPY = origin.Y + v.RowIndex * voxelSize;
                                double dblGPZ = v.BottomElevation;
                                XYZ pt0 = new XYZ(dblGPX, dblGPY, dblGPZ);
                                XYZ pt1 = pt0 + new XYZ(voxelSize, 0, 0);
                                XYZ pt2 = pt1 + new XYZ(0, voxelSize, 0);
                                XYZ pt3 = pt2 - new XYZ(voxelSize, 0, 0);
                                Line l0 = Line.CreateBound(pt0, pt1);
                                Line l1 = Line.CreateBound(pt1, pt2);
                                Line l2 = Line.CreateBound(pt2, pt3);
                                Line l3 = Line.CreateBound(pt3, pt0);
                                CurveLoop crvLoop = new CurveLoop();
                                crvLoop.Append(l0);
                                crvLoop.Append(l1);
                                crvLoop.Append(l2);
                                crvLoop.Append(l3);
                                var crvLoops = new List<CurveLoop>() { crvLoop };
                                double dblExtrusionDist = Math.Max(1 / 304.8, v.TopElevation - v.BottomElevation);
                                Solid sld = GeometryCreationUtilities.CreateExtrusionGeometry(crvLoops, XYZ.BasisZ, dblExtrusionDist);
                                solids.Add(sld);
                                DirectShape dsGp = null;
#if Revit2016
                            dsGp = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel), new Guid().ToString(), new Guid().ToString());
#else
                                dsGp = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
#endif
                                dsGp.AppendShape(solids.ToArray());
                                //dsGp.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(String.Format("Name:{0};Category:{1};Id:{2}", ve.Name, ve.Category, ve.ElementId));
                                dsGp.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(String.Format("Index{0}, col:{1};row:{2};boundary:{3};BottomActivator:{4};TopActivator;{5};Boundary Activator{6},TopOutside:{7};BtmOutside:{8}",
                                v.Index, v.ColIndex, v.RowIndex, v.IsBoundaryVoxel, v.BottomActivater, v.TopActivater, v.BoundaryActivater, v.TopOutside, v.BottomOutside));
                                if(v.IsBoundaryVoxel)
                                {
                                    MultiVerTool.ModifyOutfit(doc.ActiveView, dsGp.Id, solidFill.Id, new Color(255, 0, 0), 10);
                                    VoxelIds2Remove.Add(dsGp.Id);
                                }
                                else
                                {
                                    MultiVerTool.ModifyOutfit(doc.ActiveView, dsGp.Id, solidFill.Id, new Color(0, 255, 0), 10);
                                    VoxelIds2Remove.Add(dsGp.Id);
                                }
                            }
                        }
                    }
                    else
                    {
                        List<Solid> solids = new List<Solid>();
                        foreach (var v in Owner.Voxel2Show)
                        {
                            //create voxel solid
                            double dblGPX = origin.X + v.ColIndex * voxelSize;
                            double dblGPY = origin.Y + v.RowIndex * voxelSize;
                            double dblGPZ = v.BottomElevation;
                            XYZ pt0 = new XYZ(dblGPX, dblGPY, dblGPZ);
                            XYZ pt1 = pt0 + new XYZ(voxelSize, 0, 0);
                            XYZ pt2 = pt1 + new XYZ(0, voxelSize, 0);
                            XYZ pt3 = pt2 - new XYZ(voxelSize, 0, 0);
                            Line l0 = Line.CreateBound(pt0, pt1);
                            Line l1 = Line.CreateBound(pt1, pt2);
                            Line l2 = Line.CreateBound(pt2, pt3);
                            Line l3 = Line.CreateBound(pt3, pt0);
                            CurveLoop crvLoop = new CurveLoop();
                            crvLoop.Append(l0);
                            crvLoop.Append(l1);
                            crvLoop.Append(l2);
                            crvLoop.Append(l3);
                            var crvLoops = new List<CurveLoop>() { crvLoop };
                            double dblExtrusionDist = Math.Max(1 / 304.8, v.TopElevation - v.BottomElevation);
                            Solid sld = GeometryCreationUtilities.CreateExtrusionGeometry(crvLoops, XYZ.BasisZ, dblExtrusionDist);
                            solids.Add(sld);
                        }
                        DirectShape dsGp = null;
#if Revit2016
                        dsGp = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel), new Guid().ToString(), new Guid().ToString());
#else
                        dsGp = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
#endif
                        dsGp.AppendShape(solids.ToArray());
                        MultiVerTool.ModifyOutfit(doc.ActiveView, dsGp.Id, solidFill.Id, new Color(0, 255, 0), 90);
                        VoxelIds2Remove.Add(dsGp.Id);
                    }
                    t.Commit();
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Revit", ex.Message + ex.StackTrace);
            }
        }

        public string GetName()
        {
            return "Visualize elements";
            //throw new NotImplementedException();
        }
    }
   

    

    public class ShowResultHandler : IExternalEventHandler
    {
        public static XYZ Origin { get; set; }
        public static double VoxelSize { get; set; }
        public static double voxelVerticalSize { get; set; }
        public static bool ShowBox { get; set; }
        public static Dictionary<string, List<Solid>> BoxSolids = new Dictionary<string, List<Solid>>();
        public static Dictionary<string, List<CellIndex3D>> VoxMissing { get; set; }
        public static Dictionary <string,List<CellIndex3D>> VoxRedundancy { get; set; }

        public static Dictionary <string,MeshElement> MeshElems { get; set; }
        private List<ElementId> elemIds2Remove = new List<ElementId>();

        public static string ElemId2Visualize { get; set; } = String.Empty;
        public void Execute(UIApplication app)
        {
            try
            {
                var doc = app.ActiveUIDocument.Document;
                //Generaete voxel
                FillPatternElement solidFill = null;
                FilteredElementCollector coll = new FilteredElementCollector(doc).OfClass(typeof(FillPatternElement));
                foreach (FillPatternElement fpe in coll)
                {
                    if (fpe.GetFillPattern().IsSolidFill)
                    {
                        solidFill = fpe;
                    }
                }
                using(Transaction t=new Transaction (doc))
                {
                    t.Start("Visualize result");
                    foreach (var elemId in elemIds2Remove)
                    {
                        if (doc.GetElement(elemId) != null)
                        {
                            doc.Delete(elemId);
                        }
                    }
                    elemIds2Remove.Clear();
                    //load voxelMissing
                    if(VoxMissing.ContainsKey(ElemId2Visualize))
                    {
                        var kvp=VoxMissing[ElemId2Visualize];
                        string strElemId = ElemId2Visualize;
                        List<Solid> sldMissing = new List<Solid>();
                        if(kvp.Count !=0)
                        {
                            foreach (var cix in kvp)
                            {
                                sldMissing.Add(cix2Solid(cix));

                            }
                            DirectShape dsGp = null;
#if Revit2016
                dsGp = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel), new Guid().ToString(), new Guid().ToString());
#else
                            dsGp = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
#endif
                            dsGp.AppendShape(sldMissing.ToArray());
                            dsGp.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set("Missing voxels(" + strElemId + ")");
                            MultiVerTool.ModifyOutfit(doc.ActiveView, dsGp.Id, solidFill.Id, new Color(0, 0, 255), 0);
                            elemIds2Remove.Add(dsGp.Id);
                        }
                    }
                    //load voxelRedundancy
                    if(VoxRedundancy.ContainsKey(ElemId2Visualize))
                    {
                        var kvp = VoxRedundancy[ElemId2Visualize];
                        string strElemId = ElemId2Visualize;
                        List<Solid> sldRed = new List<Solid>();
                        if(kvp.Count !=0)
                        {
                            foreach (var cix in kvp)
                            {
                                sldRed.Add(cix2Solid(cix));

                            }
                            DirectShape dsGp = null;
#if Revit2016
                dsGp = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel), new Guid().ToString(), new Guid().ToString());
#else
                            dsGp = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
#endif
                            dsGp.AppendShape(sldRed.ToArray());
                            dsGp.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set("Redundancy voxels(" + strElemId + ")");
                            MultiVerTool.ModifyOutfit(doc.ActiveView, dsGp.Id, solidFill.Id, new Color(255, 0, 0), 0);
                            elemIds2Remove.Add(dsGp.Id);
                        }
                        
                    }
                    //load triangles
                    if(MeshElems.ContainsKey(ElemId2Visualize))
                    {
                        WireframeBuilder triBuilder = new WireframeBuilder();

                        var me = MeshElems[ElemId2Visualize];
                        foreach (var sld in me.Solids)
                        {
                            foreach (var tri in sld.Triangles)
                            {
                                List<Line> edges = new List<Line>();
                                List<Point> pts = new List<Point>();
                                for(int i=0;i<=2;i++)
                                {
                                    XYZ pt0 = Vec32XYZ(tri.Get_Vertex(i % 3));
                                    XYZ pt1 = Vec32XYZ(tri.Get_Vertex((i + 1) % 3));
                                    var len = (pt1 - pt0).GetLength();
                                    if (len <= app.Application.ShortCurveTolerance)
                                    {
                                        continue;
                                    }
                                    Line li = Line.CreateBound(pt0, pt1);
                                    Point pt = Point.Create(pt0);
                                    pts.Add(pt);
                                    triBuilder.AddCurve(li);
                                    triBuilder.AddPoint(pt);
                                }
                            }
                        }
                        DirectShape dsGp = null;

#if Revit2016
                dsGp = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel), new Guid().ToString(), new Guid().ToString());
#else
                        dsGp = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
#endif
                        dsGp.AppendShape(triBuilder);
                        dsGp.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(me.ElementId);
                       // MultiVerTool.ModifyOutfit(doc.ActiveView, dsGp.Id, solidFill.Id, new Color(0, 255, 0), 90);
                        elemIds2Remove.Add(dsGp.Id);
                    }
                    if(ShowBox && BoxSolids!=null)
                    {
                        if(BoxSolids.ContainsKey (ElemId2Visualize))
                        {
                            var kvp = BoxSolids[ElemId2Visualize];
                            DirectShape dsGp = null;

#if Revit2016
                dsGp = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel), new Guid().ToString(), new Guid().ToString());
#else
                            dsGp = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
#endif
                            dsGp.AppendShape(kvp.ToArray());
                            dsGp.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set("Boxes(" + kvp + ")");
                            MultiVerTool.ModifyOutfit(doc.ActiveView, dsGp.Id, solidFill.Id, new Color(0, 255, 0), 90);
                            elemIds2Remove.Add(dsGp.Id);
                        }
                    }
                    t.Commit();
                }
                if(elemIds2Remove .Count !=0)
                {
                    app.ActiveUIDocument.ShowElements(elemIds2Remove);
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace);
            }
        }
        private XYZ Vec32XYZ(Vec3 v)
        {
            return new XYZ(v.X, v.Y, v.Z);
        }
        private Solid cix2Solid(CellIndex3D cix)
        {
            var col = cix.Col;
            var row = cix.Row;
            var layer = cix.Layer;
            XYZ pt0 = Origin + new XYZ(col * VoxelSize, row * VoxelSize, layer * voxelVerticalSize);
            XYZ pt1 = pt0 + XYZ.BasisX * VoxelSize;
            XYZ pt2 = pt1 + XYZ.BasisY * VoxelSize;
            XYZ pt3 = pt2 - XYZ.BasisX * VoxelSize;
            XYZ[] pts = new XYZ[] { pt0, pt1, pt2, pt3 };
            CurveLoop loop = new CurveLoop();
            List<CurveLoop> loops = new List<CurveLoop>() { loop};
            for(int i=0;i<=3;i++)
            {
                Line li = Line.CreateBound(pts[(i) % 4], pts[(i + 1) % 4]);
                loop.Append(li);
            }
            Solid sld = GeometryCreationUtilities.CreateExtrusionGeometry(loops, XYZ.BasisZ, voxelVerticalSize);
            return sld;
        }
        public string GetName()
        {
            return "Show test result";
            //throw new NotImplementedException();
        }
    }


    public class ShowEachTriangles : IExternalEventHandler
    {
        public static MeshElement Me { get; set; }
        public void Execute(UIApplication app)
        {
            if(Me!=null)
            {
                var doc = app.ActiveUIDocument.Document;
                var me = Me;
                using (TransactionGroup tg=new TransactionGroup (doc))
                {
                    List<ElementId> tris = new List<ElementId>();
                    tg.Start("Generating triangles");
                    using(Transaction t=new Transaction(doc))
                    {
                        t.Start("Generate each triangles");
                        foreach (var sld in me.Solids)
                        {
                            WireframeBuilder triBuilder = new WireframeBuilder();
                            foreach (var tri in sld.Triangles)
                            {
                                List<Line> edges = new List<Line>();
                                List<Point> pts = new List<Point>();
                                for (int i = 0; i <= 2; i++)
                                {
                                    XYZ pt0 = Vec32XYZ(tri.Get_Vertex(i % 3));
                                    XYZ pt1 = Vec32XYZ(tri.Get_Vertex((i + 1) % 3));
                                    var len = (pt1 - pt0).GetLength();
                                    if (len <= app.Application.ShortCurveTolerance)
                                    {
                                        continue;
                                    }
                                    Line li = Line.CreateBound(pt0, pt1);
                                    Point pt = Point.Create(pt0);
                                    pts.Add(pt);
                                    triBuilder.AddCurve(li);
                                    triBuilder.AddPoint(pt);
                                }
                                if(pts.Count ==3)
                                {
                                    DirectShape dsGp = null;
#if Revit2016
        dsGp = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel), new Guid().ToString(), new Guid().ToString());
#else
                                    dsGp = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
#endif
                                    dsGp.AppendShape(triBuilder);
                                    dsGp.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set
                                        (String.Format ("{0},{1},{2}"  ,tri.Get_Vertex(0).ToString (),tri.Get_Vertex(1).ToString (),tri.Get_Vertex(2).ToString ()));
                                    tris.Add(dsGp.Id);
                                }
                            }
                        }
                        t.Commit();
                        using(Transaction t2=new Transaction (doc))
                        {
                            t2.Start("Group triangles");
                            doc.Create.NewGroup(tris);

                            t2.Commit();
                        }
                    
                    
                    
                    
                    
                    }
                    


                    tg.Assimilate();
                    // MultiVerTool.ModifyOutfit(doc.ActiveView, dsGp.Id, solidFill.Id, new Color(0, 255, 0), 90);
                }

            }

            //throw new NotImplementedException();
        }
        private XYZ Vec32XYZ(Vec3 v)
        {
            return new XYZ(v.X, v.Y, v.Z);
        }
        public string GetName()
        {
            return "Show Triangle";
            //throw new NotImplementedException();
        }
    }

    public class ShowTriangleHandler : IExternalEventHandler
    {
        public static List<MeshElement> Mesh2Show { get; set; }
        public void Execute(UIApplication app)
        {
            try
            {
                var doc = app.ActiveUIDocument.Document;
                var view = doc.ActiveView;
                if (Mesh2Show != null)
                {
                   using (TransactionGroup tg=new TransactionGroup (doc))
                   {
                        List<ElementId> tris = new List<ElementId>();
                        tg.Start("Generating triangles");
                        using(Transaction t=new Transaction(doc))
                        {
                            t.Start("Generate each triangles");
                            foreach (var me in Mesh2Show)
                            {
                                foreach (var sld in me.Solids)
                                {
                                    WireframeBuilder triBuilder = new WireframeBuilder();
                                    foreach (var tri in sld.Triangles)
                                    {
                                        List<Line> edges = new List<Line>();
                                        List<Point> pts = new List<Point>();
                                        for (int i = 0; i <= 2; i++)
                                        {
                                            XYZ pt0 = Vec32XYZ(tri.Get_Vertex(i % 3));
                                            XYZ pt1 = Vec32XYZ(tri.Get_Vertex((i + 1) % 3));
                                            var len = (pt1 - pt0).GetLength();
                                            if (len <= app.Application.ShortCurveTolerance)
                                            {
                                                continue;
                                            }
                                            Line li = Line.CreateBound(pt0, pt1);
                                            Point pt = Point.Create(pt0);
                                            pts.Add(pt);
                                            triBuilder.AddCurve(li);
                                            triBuilder.AddPoint(pt);
                                        }
                                        if (pts.Count == 3)
                                        {
                                            DirectShape dsGp = null;
#if Revit2016
            dsGp = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel), new Guid().ToString(), new Guid().ToString());
#else
                                            dsGp = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
#endif
                                            dsGp.AppendShape(triBuilder);
                                            dsGp.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set
                                                (String.Format("{0},{1},{2}", tri.VerticesIndex[0], tri.VerticesIndex[1], tri.VerticesIndex[2]));
                                            tris.Add(dsGp.Id);
                                        }
                                    }
                                }
                            }
                            
                            t.Commit();
                            using(Transaction t2=new Transaction (doc))
                            {
                                t2.Start("Group triangles");
                                doc.Create.NewGroup(tris);

                                t2.Commit();
                            }
                         }
                    tg.Assimilate();
                    // MultiVerTool.ModifyOutfit(doc.ActiveView, dsGp.Id, solidFill.Id, new Color(0, 255, 0), 90);
                    }

                }
                //throw new NotImplementedException();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace); 
            }

        }
        private XYZ Vec32XYZ(Vec3 v)
        {
            return new XYZ(v.X, v.Y, v.Z);
        }
        public string GetName()
        {
            return "Show triangles";
           // throw new NotImplementedException();
        }
    }
    
    public static class MultiVerTool
    {
        public static void ModifyOutfit(Autodesk.Revit.DB.View view,ElementId elemId, ElementId fillPatternId,Color color, int transparency)
        {
            var setting = view.GetElementOverrides(elemId);
            #if Revit2016 || Revit2018
            setting.SetProjectionFillColor(color);
            setting.SetProjectionFillPatternId(fillPatternId);
            setting.SetSurfaceTransparency(90);
#else
            setting.SetSurfaceForegroundPatternColor(color);
            setting.SetSurfaceForegroundPatternId(fillPatternId);
#endif
            setting.SetSurfaceTransparency(transparency);
            view.SetElementOverrides(elemId, setting);
        }
    }

    public class ExportSolidInModel:IExternalEventHandler
    {
        public  FrmVoxel Owner { get; set; }

        public ExportSolidInModel(FrmVoxel frm)
        {
            Owner = frm;
        }
        public void Execute(UIApplication app)
        {
            try
            {
                bool outputElemnetBySolid = Owner.OutputElementBySolid;
                var doc = app.ActiveUIDocument.Document;
                var view = doc.ActiveView;
                var option = new Options();
                option.IncludeNonVisibleObjects = false;
                option.View = view;
                var elems = new FilteredElementCollector(doc, view.Id).WhereElementIsNotElementType().ToList();
                int numElem = elems.Count;
                Owner.InitProgress(0, numElem);
                var docPath = doc.PathName;
                var saveAsPath = docPath + "_VoxValidation.rvt";
                using(TransactionGroup tg=new TransactionGroup (doc))
                {
                    //collect existing elements to remove them
                    List<ElementId> existingElemIds = new List<ElementId>();
                    tg.Start("Create element");
                    List<ElementId> elemId = new List<ElementId>();
                    using (Transaction t = new Transaction(doc))
                    {
                        t.Start("Create element");
                        int i = 0;
                        foreach (var elem in elems)
                        {
                            existingElemIds.Add(elem.Id);
                            List<Solid> elemSolids = GetSolidInElement(elem, view).ToList();
                            foreach (var dsGp in this.CreateDirectShapeElement(elem,view, outputElemnetBySolid))
                            {
                                elemId.Add(dsGp.Id);
                            }
                            i += 1;
                            Owner.UpdateProgress(string.Format("{0} elements Created, total{1}", i, elems.Count));
                        }
                        t.Commit();
                    }
                    using (Transaction t0 = new Transaction(doc))
                    {
                        t0.Start("Create group");
                        Group gp = doc.Create.NewGroup(elemId);
                        //hide element
                        List<ElementId> elemId2Hide = new List<ElementId>();
                        foreach (var eid in existingElemIds)
                        {
                            var elem = doc.GetElement(eid);
                            if (elem.CanBeHidden(view))
                            {
                                elemId2Hide.Add(eid);
                            }
                        }
                        view.HideElements(elemId2Hide);
                        t0.Commit();
                    }
                    
                    tg.Assimilate();
                }
                //save new files
                doc.SaveAs(saveAsPath,new SaveAsOptions() { OverwriteExistingFile=true});
                MessageBox.Show("New element created successfully");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message+ex.StackTrace);
            }

            //throw new NotImplementedException();
        }
        private IEnumerable<DirectShape> CreateDirectShapeElement(Element elem, View view, bool generateElementBySolid)
        {
            var doc = elem.Document;
            List<Solid> elemSolids = GetSolidInElement(elem, view).ToList();
            if (elemSolids.Count > 0)//
            {
                DirectShape dsGp = null;
                if (!generateElementBySolid)
                {
                   
#if Revit2016
                         dsGp = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel), new Guid().ToString(), new Guid().ToString());
#else
                    dsGp = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
#endif
                    //dsGp.Name = elem.Name;
                    dsGp.AppendShape(elemSolids.Where(c => dsGp.IsValidGeometry(c)).ToArray());
                    dsGp.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(String.Format("Name:{0};Category:{1};Id:{2}", elem.Name, elem.Category.Name, elem.Id));
                    yield return dsGp;
                }
                else //generate element by solid
                {
                    foreach (var sld in elemSolids)
                    {
#if Revit2016
                         dsGp = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel), new Guid().ToString(), new Guid().ToString());
#else
                        dsGp = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
#endif
                        if(dsGp.IsValidGeometry(sld))
                        {
                            //dsGp.Name = elem.Name;
                            dsGp.AppendShape(new Solid[1] {sld});
                            dsGp.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(String.Format("Name:{0};Category:{1};Id:{2}", elem.Name, elem.Category.Name, elem.Id));
                            yield return dsGp;
                        }
                    }
                    
                }
            }
        } 

        public IEnumerable<Solid> GetSolidInElement(Element elem,View view)
        {
            var option = new Options();
            option.IncludeNonVisibleObjects = false;
            option.View = view;
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

        public string GetName()
        {
            return "Convert element";
           // throw new NotImplementedException();
        }
    }




    [Transaction(TransactionMode.Manual)]
    public class ExportSolidAsElementInModel:IExternalCommand
    {
       
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            try
            {
                var app = data.Application;
                var doc = app.ActiveUIDocument.Document;
                var view = doc.ActiveView;
                var option = new Options();
                option.IncludeNonVisibleObjects = false;
                option.View = view;
                var elems = new FilteredElementCollector(doc, view.Id).WhereElementIsNotElementType().ToList();
                int numElem = elems.Count;
               
                var docPath = doc.PathName;
                var saveAsPath = docPath + "_VoxValidation.rvt";
                using (TransactionGroup tg = new TransactionGroup(doc))
                {
                    //collect existing elements to remove them
                    List<ElementId> existingElemIds = new List<ElementId>();
                    tg.Start("Create element");
                    List<ElementId> elemId = new List<ElementId>();
                    using (Transaction t = new Transaction(doc))
                    {
                        t.Start("Create element");
                        foreach (var elem in elems)
                        {
                            existingElemIds.Add(elem.Id);
                            List<Solid> elemSolids = GetSolidInElement(elem, view).ToList();
                            foreach (var sld in elemSolids)//
                            {
                                DirectShape dsGp = null;
#if Revit2016
                         dsGp = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel), new Guid().ToString(), new Guid().ToString());
#else
                                dsGp = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
#endif
                                if(dsGp.IsValidGeometry(sld))
                                {
                                    //dsGp.Name = elem.Name;
                                    dsGp.AppendShape(new Solid[1] {sld});
                                    dsGp.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(String.Format("Name:{0};Category:{1};Id:{2}", elem.Name, elem.Category.Name, elem.Id));
                                }
                                elemId.Add(dsGp.Id);
                            }
                        }
                        //hide element
                        List<ElementId> elemId2Hide = new List<ElementId>();
                        foreach (var eid in existingElemIds)
                        {
                            var elem = doc.GetElement(eid);
                            if (elem.CanBeHidden(view))
                            {
                                elemId2Hide.Add(eid);
                            }
                        }
                        view.HideElements(elemId2Hide);
                        t.Commit();
                    }
                    if(doc.IsFamilyDocument==false)
                    {
                        using (Transaction t0 = new Transaction(doc))
                        {
                            t0.Start("Create group");
                            Group gp = doc.Create.NewGroup(elemId);
                           
                            t0.Commit();
                        }
                    }
                    

                    tg.Assimilate();
                }
                //save new files
                doc.SaveAs(saveAsPath, new SaveAsOptions() { OverwriteExistingFile = true });
                MessageBox.Show("New element created successfully");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace);
                return Result.Failed;
            }

            //throw new NotImplementedException();
        }

        public IEnumerable<Solid> GetSolidInElement(Element elem, View view)
        {
            var option = new Options();
            option.IncludeNonVisibleObjects = false;
            option.View = view;
            GeometryElement geoElem = elem.get_Geometry(option);
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
                    else if (geoObject is GeometryInstance)
                    {
                        var geoIns = geoObject as GeometryInstance;
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
                    else if (geoObject is GeometryInstance)
                    {
                        var geoIns = geoObject as GeometryInstance;
                        foreach (var sld in GetSolidInGeometryInstance(geoIns))
                        {
                            yield return sld;
                        }
                    }
                }
            }
        }

        public string GetName()
        {
            return "Convert element";
            // throw new NotImplementedException();
        }
    }
    [Transaction(TransactionMode.Manual)]
    public class ExportAssemblyInstanceAsElementInModel : IExternalCommand
    {

        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            try
            {
                var app = data.Application;
                var doc = app.ActiveUIDocument.Document;
                var view = doc.ActiveView;
                var option = new Options();
                option.IncludeNonVisibleObjects = false;
                option.View = view;
                var elems = new FilteredElementCollector(doc, view.Id).WhereElementIsNotElementType().OfClass(typeof(AssemblyInstance)).ToList();
                int numElem = elems.Count;
                //var saveAsPath = docPath + "_VoxValidation.rvt";
                //use a dictionary to store solids in each assembly
                Dictionary<string,Tuple<List<Solid>, List<Autodesk.Revit.DB.Transform>>> dicAssembly_Slds_Trfs = new Dictionary<string, Tuple<List<Solid>, List<Autodesk.Revit.DB.Transform>>>();
                using (TransactionGroup tg = new TransactionGroup(doc))
                {
                    //collect existing elements to remove them
                    List<ElementId> existingElemIds = new List<ElementId>();
                    tg.Start("Create element");
                    List<ElementId> elemId = new List<ElementId>();
                    using (Transaction t = new Transaction(doc))
                    {
                        t.Start("Create element");
                        foreach (var elem in elems)
                        {
                            existingElemIds.Add(elem.Id);
                            var aIns= elem as AssemblyInstance;
                            string assemblyName = aIns.AssemblyTypeName;
                            var trfIns = aIns.GetTransform();
                            if (!dicAssembly_Slds_Trfs.ContainsKey(assemblyName))
                            {
                                List<Solid> sldAssSymbol = new List<Solid>();
                                existingElemIds.AddRange(aIns.GetMemberIds());
                                foreach (var compId in aIns.GetMemberIds())
                                {
                                    var compElem=doc.GetElement (compId);
                                    List<Solid> elemSolids = GetSolidInElement(compElem, view).ToList();
                                    sldAssSymbol.AddRange(elemSolids);

                                }
                                Tuple<List<Solid>, List<Autodesk.Revit.DB.Transform>> sld_Transform = new Tuple<List<Solid>, List<Autodesk.Revit.DB.Transform>>(sldAssSymbol, new List<Autodesk.Revit.DB.Transform>() { trfIns });
                                dicAssembly_Slds_Trfs.Add (assemblyName, sld_Transform);
                            }
                            else
                            {
                                var sld_Trf = dicAssembly_Slds_Trfs[assemblyName];
                                var trfBase = sld_Trf.Item2[0];
                                var trfNew = trfIns.Multiply(trfBase.Inverse);
                                sld_Trf.Item2.Add(trfNew);
                            }
                           
                        }
                        //create elementts
                        foreach (var item in dicAssembly_Slds_Trfs)
                        {
                            var elemName = item.Key;
                            var elemSolids = item.Value.Item1;
                            DirectShapeLibrary dsLib = DirectShapeLibrary.GetDirectShapeLibrary(doc);
                            
                           
                            var defTypeId = dsLib.FindDefinitionType(elemName);
                            item.Value.Item2[0] = Autodesk.Revit.DB.Transform.Identity;
                            foreach (var trf in item.Value.Item2)
                            {
                                dsLib.AddDefinition(elemName, elemSolids.ToArray());
                                //var geoObj= DirectShape.CreateGeometryInstance(doc, elemName, trf);
#if Revit2016
                                DirectShape dsGp = DirectShape.CreateElementInstance(doc, defTypeId, new ElementId(BuiltInCategory.OST_GenericModel), elemName, trf,new Guid().ToString (),new Guid().ToString());
#else
                                DirectShape dsGp = DirectShape.CreateElementInstance(doc, defTypeId, new ElementId(BuiltInCategory.OST_GenericModel), elemName, trf);
#endif
                                dsGp.Name = elemName;
                            }
                        }
                        //hide element
                        List<ElementId> elemId2Hide = new List<ElementId>();
                        foreach (var eid in existingElemIds)
                        {
                            var elem = doc.GetElement(eid);
                            if (elem.CanBeHidden(view))
                            {
                                elemId2Hide.Add(eid);
                            }
                        }
                        view.HideElements(elemId2Hide);
                        t.Commit();
                    }
                    
                    tg.Assimilate();
                }
                //save new files
                //doc.SaveAs(saveAsPath, new SaveAsOptions() { OverwriteExistingFile = true });
                MessageBox.Show("New element created successfully");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace);
                return Result.Failed;
            }

            //throw new NotImplementedException();
        }

        public IEnumerable<Solid> GetSolidInElement(Element elem, View view)
        {
            var option = new Options();
            option.IncludeNonVisibleObjects = false;
            option.View = view;
            GeometryElement geoElem = elem.get_Geometry(option);
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
                    else if (geoObject is GeometryInstance)
                    {
                        var geoIns = geoObject as GeometryInstance;
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
                    else if (geoObject is GeometryInstance)
                    {
                        var geoIns = geoObject as GeometryInstance;
                        foreach (var sld in GetSolidInGeometryInstance(geoIns))
                        {
                            yield return sld;
                        }
                    }
                }
            }
        }

        public string GetName()
        {
            return "Convert element";
            // throw new NotImplementedException();
        }
    }



    [Transaction(TransactionMode.Manual)]
    public class SnoopMeshes : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = commandData.Application.ActiveUIDocument.Document;
            System.Windows.Forms.OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Mesh element|*.voxMesh";
            if(ofd.ShowDialog() == DialogResult.OK)
            {
                TempFileSaver saver = new TempFileSaver();
                double dblXmax = double.MinValue;
                double dblYmax = double.MinValue;
                double dblZmax = double.MinValue;
                double dblXmin = double.MaxValue;
                double dblYmin = double.MaxValue;
                double dblZmin = double.MaxValue;
               // Transaction t = new Transaction(doc);
                //t.Start("snoop elements");
                foreach (var me in saver.ReadMeshElement(ofd.FileName, false))
                {
                    WireframeBuilder builder = new WireframeBuilder();
                    foreach (var sld in me.Solids)
                    {
                        foreach (var xyz in sld.Vertices)
                        {
                            dblXmax = Math.Max(xyz.X, dblXmax);
                            dblYmax = Math.Max(xyz.Y, dblYmax);
                            dblZmax = Math.Max(xyz.Z, dblZmax);

                            dblXmin = Math.Min(xyz.X, dblXmin);
                            dblYmin = Math.Min(xyz.Y, dblYmin);
                            dblZmin = Math.Min(xyz.Z, dblZmin);

                            //Point pt = Point.Create(new XYZ(xyz.X, xyz.Y, xyz.Z));
                            //builder.AddPoint(pt);
                        }
                        /*
                        //scan triangle
                        foreach (var tri in sld.Triangles)
                        {
                            for(int i=0;i<=2;i++)
                            {
                                var pt0 = tri.Get_Vertex(i % 3);
                                var pt1 = tri.Get_Vertex((i + 1) % 3);
                                if((pt1-pt0).GetLength()>commandData.Application.Application.ShortCurveTolerance)
                                {
                                    XYZ p0 = new XYZ(pt0.X, pt0.Y, pt0.Z);
                                    XYZ p1 = new XYZ(pt1.X, pt1.Y, pt1.Z);
                                    Line li = Line.CreateBound(p0, p1);
                                    builder.AddCurve(li);
                                }
                            }
                        }
                        */
                    }
                    /*
                    DirectShape dsGp = null;
#if Revit2016
                         dsGp = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel), new Guid().ToString(), new Guid().ToString());
#else
                    dsGp = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
#endif
                    //dsGp.Name = elem.Name;
                    dsGp.AppendShape(builder);
                    dsGp.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(String.Format("Name:{0};Category:{1};Id:{2}", me.Name, me.Category, me.ElementId));
                    */
                }
                //t.Commit();
                MessageBox.Show(string.Format("Bounding box scale:{0}m * {1}m *{2}m",
                                ((dblXmax - dblXmin) * 0.3048).ToString("0.00"),
                                ((dblYmax - dblYmin) * 0.3048).ToString("0.00"),
                                ((dblZmax - dblZmin) * 0.3048).ToString("0.00")));
            }
            return Result.Succeeded;
        }
    }
    

    
    public class LoadCompressedVoxelFiles : IExternalEventHandler
    {
        public static CompressedVoxelDocument cvdoc { get; set; }
        public void Execute(UIApplication app)
        {
            var doc = app.ActiveUIDocument.Document;
            using (Transaction t = new Transaction(doc))
            {
                
                t.Start("Show Rectangle");
                var dicHeight = cvdoc.VoxelHight;
                Vec3 origin = cvdoc.Origin;
                double voxelSize = cvdoc.VoxelSize;
                FillPatternElement solidFill = null;
                FilteredElementCollector coll = new FilteredElementCollector(doc).OfClass(typeof(FillPatternElement));
                foreach (FillPatternElement fpe in coll)
                {
                    if (fpe.GetFillPattern().IsSolidFill)
                    {
                        solidFill = fpe;
                    }
                }
                foreach (var elem in cvdoc.Elements)
                {
                    List<Solid> slds = new List<Solid>();
                    foreach (var rect in elem.VoxelRectangles)
                    {
                        var scale = rect.Get_Scale(cvdoc.VoxelScale);

                        Vec3 locOffset = new Vec3(rect.Start.Col * voxelSize, rect.Start.Row * voxelSize, rect.BottomElevation / 304.8);
                        Vec3 loc = origin + locOffset;
                        Vec3 min = loc;
                        Vec3 max = loc + new Vec3((scale.Col + 1) * voxelSize, (scale.Row + 1) * voxelSize, scale.Layer / 304.8);
                        Solid boxSld = GeometryTool.GetSolid(min, max);
                        slds.Add(boxSld);
                    }
                    DirectShape dsGp = null;
#if Revit2016
                            dsGp = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel), new Guid().ToString(), new Guid().ToString());
#else
                    dsGp = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
#endif
                    dsGp.AppendShape(slds.ToArray());
                    dsGp.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(String.Format("Name:{0};Category:{1};Id:{2}", elem.Name, elem.Category, elem.ElementId));
                    //dsGp.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(String.Format("Index{0}, col:{1};row:{2};boundary:{3};BottomActivator:{4};TopActivator;{5};Boundary Activator{6},TopOutside:{7};BtmOutside:{8}",
                    //v.Index, v.ColIndex, v.RowIndex, v.IsBoundaryVoxel, v.BottomActivater, v.TopActivater, v.BoundaryActivater, v.TopOutside, v.BottomOutside));
                    MultiVerTool.ModifyOutfit(doc.ActiveView, dsGp.Id, solidFill.Id, new Color(0, 255, 0), 10);
                }
                t.Commit();
            }
           
            //throw new NotImplementedException();
        }

        public string GetName()
        {
            return "Visualize";
            //throw new NotImplementedException();
        }
    }
    public class ShowRawAccessibleRegion : IExternalEventHandler
    {
        public static CompressedVoxelDocument cvdoc { get; set; }
        public static List<AccessibleRectangle> Ars2Show { get; set; }
        public void Execute(UIApplication app)
        {
            var doc = app.ActiveUIDocument.Document;
            try
            {
                using (Transaction t = new Transaction(doc))
                {

                    t.Start("Show ARs");
                    var dicHeight = cvdoc.VoxelHight;
                    Vec3 origin = cvdoc.Origin;
                    double voxelSize = cvdoc.VoxelSize;
                    FillPatternElement solidFill = null;
                    FilteredElementCollector coll = new FilteredElementCollector(doc).OfClass(typeof(FillPatternElement));
                    foreach (FillPatternElement fpe in coll)
                    {
                        if (fpe.GetFillPattern().IsSolidFill)
                        {
                            solidFill = fpe;
                        }
                    }
                    foreach (var ar in Ars2Show)
                    {
                        List<Solid> slds = new List<Solid>();
                        Vec3 min = new Vec3(ar.Min.Col * voxelSize, ar.Min.Row * voxelSize, ar.Elevation);
                        Vec3 max = new Vec3((ar.Max.Col + 1) * voxelSize, (ar.Max.Row + 1) * voxelSize, ar.Elevation + 1 / 304.8);
                        Solid boxSld = GeometryTool.GetSolid(min, max);
                        List<int> arNeighborIndex = new List<int>();
                        foreach (var neighbour in ar.AdjacentRectangles)
                        {
                            arNeighborIndex.Add(neighbour.Index);
                        }
                        string strAdjInfo = "Adj:" + string.Join(";", arNeighborIndex);
                        slds.Add(boxSld);
                        DirectShape dsGp = null;
#if Revit2016
                            dsGp = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel), new Guid().ToString(), new Guid().ToString());
#else
                        dsGp = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
#endif
                        dsGp.AppendShape(slds.ToArray());
                        dsGp.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set( "Index:" +ar.Index.ToString()+";"+ strAdjInfo);
                        //dsGp.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(String.Format("Index{0}, col:{1};row:{2};boundary:{3};BottomActivator:{4};TopActivator;{5};Boundary Activator{6},TopOutside:{7};BtmOutside:{8}",
                        //v.Index, v.ColIndex, v.RowIndex, v.IsBoundaryVoxel, v.BottomActivater, v.TopActivater, v.BoundaryActivater, v.TopOutside, v.BottomOutside));
                        MultiVerTool.ModifyOutfit(doc.ActiveView, dsGp.Id, solidFill.Id, new Color(0, 255, 0), 10);
                    }
                    t.Commit();
                }
                
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace);
            }

            //throw new NotImplementedException();
        }

        public string GetName()
        {
            return "Visualize AR";
            //throw new NotImplementedException();
        }
    }
    public class ShowRegionHandler : IExternalEventHandler
    {
       
        public static CompressedVoxelDocument Doc { get; set; }
        public static List<AccessibleRegion> Regions { get; set; }
        private List<ElementId> VoxelIds2Remove = new List<ElementId>();
        public void Execute(UIApplication app)
        {
            try
            {
                var doc = app.ActiveUIDocument.Document;
                using (Transaction t = new Transaction(doc))
                {
                    t.Start("Show voxels");
                    if (VoxelIds2Remove.Count != 0)
                    {
                        //if(this.Owner.RemovePath==true)
                        {
                            foreach (var voxId in VoxelIds2Remove)
                            {
                                if (doc.GetElement(voxId) != null)
                                {
                                    doc.Delete(voxId);
                                }
                            }
                        }

                    }
                    VoxelIds2Remove.Clear();
                    //Generaete voxel
                    FillPatternElement solidFill = null;
                    FilteredElementCollector coll = new FilteredElementCollector(doc).OfClass(typeof(FillPatternElement));
                    foreach (FillPatternElement fpe in coll)
                    {
                        if (fpe.GetFillPattern().IsSolidFill)
                        {
                            solidFill = fpe;
                        }
                    }
                    var origin = Doc.Origin;

                    var voxelSize = Doc.VoxelSize;
                    int index = 0;
                    foreach (var rng in Regions)
                    {
                        /*
                        List<Solid> solids = new List<Solid>();
                        foreach (var v in rng.Voxels)
                        {
                            //create voxel solid
                            double dblGPX = origin.X + v.ColIndex * voxelSize;
                            double dblGPY = origin.Y + v.RowIndex * voxelSize;
                            double dblGPZ = v.BottomElevation;
                            XYZ pt0 = new XYZ(dblGPX, dblGPY, dblGPZ);
                            XYZ pt1 = pt0 + new XYZ(voxelSize, 0, 0);
                            XYZ pt2 = pt1 + new XYZ(0, voxelSize, 0);
                            XYZ pt3 = pt2 - new XYZ(voxelSize, 0, 0);
                            Line l0 = Line.CreateBound(pt0, pt1);
                            Line l1 = Line.CreateBound(pt1, pt2);
                            Line l2 = Line.CreateBound(pt2, pt3);
                            Line l3 = Line.CreateBound(pt3, pt0);
                            CurveLoop crvLoop = new CurveLoop();
                            crvLoop.Append(l0);
                            crvLoop.Append(l1);
                            crvLoop.Append(l2);
                            crvLoop.Append(l3);
                            var crvLoops = new List<CurveLoop>() { crvLoop };
                            double dblExtrusionDist = Math.Max(1 / 304.8, v.TopElevation - v.BottomElevation);
                            Solid sld = GeometryCreationUtilities.CreateExtrusionGeometry(crvLoops, XYZ.BasisZ, dblExtrusionDist);
                            solids.Add(sld);
                        }
                        DirectShape dsGp = null;
#if Revit2016
                    dsGp = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel), new Guid().ToString(), new Guid().ToString());
#else
                        dsGp = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
#endif
                        //dsGp.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set("col:" + v.ColIndex + ",row:" + v.RowIndex);
                        dsGp.AppendShape(solids.ToArray());
                        var setting = doc.ActiveView.GetElementOverrides(dsGp.Id);
                        setting.SetProjectionFillColor(new Color(0, 255, 0));
                        setting.SetProjectionFillPatternId(solidFill.Id);
                        setting.SetSurfaceTransparency(90);
                        doc.ActiveView.SetElementOverrides(dsGp.Id, setting);
                        VoxelIds2Remove.Add(dsGp.Id);
                       */

                        if (rng.Rectangles.Count > 0)
                        {
                            List<Solid> rectSolids = new List<Solid>();
                            List<ElementId> recId = new List<ElementId>();
                            foreach (var rect in rng.Rectangles)
                            {
                                var rectRange = rect.Max - rect.Min;
                                var colRng = rectRange.Col + 1;
                                var rowRng = rectRange.Row + 1;
                                double dblColSize = colRng * voxelSize;
                                double dblRowSize = rowRng * voxelSize;
                                double dblGPX = origin.X + rect.Min.Col * voxelSize;
                                double dblGPY = origin.Y + rect.Min.Row * voxelSize;
                                double dblGPZ = rect.Elevation;
                                XYZ pt0 = new XYZ(dblGPX, dblGPY, dblGPZ);
                                XYZ pt1 = pt0 + new XYZ(dblColSize, 0, 0);
                                XYZ pt2 = pt1 + new XYZ(0, dblRowSize, 0);
                                XYZ pt3 = pt2 - new XYZ(dblColSize, 0, 0);
                                Line l0 = Line.CreateBound(pt0, pt1);
                                Line l1 = Line.CreateBound(pt1, pt2);
                                Line l2 = Line.CreateBound(pt2, pt3);
                                Line l3 = Line.CreateBound(pt3, pt0);
                                CurveLoop crvLoop = new CurveLoop();
                                crvLoop.Append(l0);
                                crvLoop.Append(l1);
                                crvLoop.Append(l2);
                                crvLoop.Append(l3);
                                var crvLoops = new List<CurveLoop>() { crvLoop };
                                double dblExtrusionDist = 1 / 304.8;
                                Solid sld = GeometryCreationUtilities.CreateExtrusionGeometry(crvLoops, XYZ.BasisZ, dblExtrusionDist);
                                rectSolids.Add(sld);
                            }
                            DirectShape dsRect = null;
#if Revit2016
                             dsRect = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel), new Guid().ToString(), new Guid().ToString());
#else
                            dsRect = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_MechanicalEquipment));
#endif
                            //dsGp.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set("col:" + v.ColIndex + ",row:" + v.RowIndex);
                            dsRect.AppendShape(rectSolids.ToArray());
                            dsRect.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set("Accessible Region:" + index.ToString());

                            MultiVerTool.ModifyOutfit(doc.ActiveView, dsRect.Id, solidFill.Id, new Color(0, 0, 255), 90);
                            VoxelIds2Remove.Add(dsRect.Id);
                            recId.Add(dsRect.Id);
                        }
                        index += 1;
                    }
                    t.Commit();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace);
            }
        }

        public string GetName()
        {
            return "Visualize elements";
            //throw new NotImplementedException();
        }
    }

    public class ShowPathHandler : IExternalEventHandler
    {
        public static List<Vec3> Path { get; set; }
        public void Execute(UIApplication app)
        {
            try
            {
                var doc = app.ActiveUIDocument.Document;

                List<GeometryObject> wayPts = new List<GeometryObject>();
                Vec3 ptPrevious = null;
                WireframeBuilder builder = new WireframeBuilder();
                foreach (var pt in Path)
                {
                    Vec3 min = pt - new Vec3(1/304.8, 1/304.8, 1/304.8);
                    Vec3 max = pt + new Vec3(1/304.8, 1/304.8 , 1/304.8);
                    Solid wayPt = GeometryTool.GetSolid(min, max);
                    wayPts.Add(wayPt);
                    if (ptPrevious != null)
                    {
                        XYZ pt0 = new XYZ(ptPrevious.X, ptPrevious.Y, ptPrevious.Z);
                        XYZ pt1 = new XYZ(pt.X, pt.Y, pt.Z);
                        if ((pt0 - pt1).GetLength() < app.Application.ShortCurveTolerance)
                        {
                            continue;
                        }
                        Line li = Line.CreateBound(pt1, pt0);
                        
                        builder.AddCurve(li);
                    }
                    ptPrevious = pt;
                }
                using (Transaction t=new Transaction(doc))
                {
                    t.Start("Generate path");
                    DirectShape dsRect = null;
#if Revit2016
            dsRect = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_MechanicalEquipment), new Guid().ToString(), new Guid().ToString());
#else
                    dsRect = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_MechanicalEquipment));
#endif
                    //dsGp.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set("col:" + v.ColIndex + ",row:" + v.RowIndex);
                    dsRect.AppendShape(wayPts.ToArray());
                    dsRect.AppendShape(builder);


                    t.Commit();
                }


            }
           catch(Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace);
            }
        }

        public string GetName()
        {
            return "Show Path";
            //throw new NotImplementedException();
        }
    }
    public static class GeometryTool
    {
        public static Solid GetSolid(Vec3 min3,Vec3 max3)
        {
            XYZ min=new XYZ(min3.X,min3.Y,min3.Z);
            XYZ max = new XYZ(max3.X, max3.Y, max3.Z);
            var scale = max - min;
            var sizeX = scale.X;
            var sizeY=scale.Y;
             var sizeZ = Math.Max ( scale.Z,1/304.8);
           
            XYZ pt0 = min;
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
   
    [Transaction(TransactionMode.Manual)]
    public class ShowTriangles : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var app = commandData.Application;
            var doc = commandData.Application.ActiveUIDocument.Document;
            var sel = commandData.Application.ActiveUIDocument.Selection;
            var elemRef = sel.PickObject(Autodesk.Revit.UI.Selection.ObjectType.Element);
            var elem = doc.GetElement(elemRef);
            List<Mesh> meshes=GetMeshInElement(elem);
            using (Transaction t = new Transaction(doc))
            {
                t.Start("generate triangles");
               
                WireframeBuilder triBuilder = new WireframeBuilder();
                foreach (var me in meshes)
                {
                    for (int i = 0; i <= me.NumTriangles - 1; i++)
                    {
                        var tri = me.get_Triangle(i);
                        
                        List<XYZ> triPts=new List<XYZ>();
                        for (int j = 0; j <= 2; j++)
                        {
                            XYZ pt0 = tri.get_Vertex(j % 3);
                            XYZ pt1 = tri.get_Vertex((j + 1) % 3);
                            var len = (pt1 - pt0).GetLength();
                            if (len <= app.Application.ShortCurveTolerance)
                            {
                                break;
                            }
                            triPts.Add(pt0);
                        }
                        if (triPts.Count == 3)
                        {
                            for (int j = 0; j <= 2; j++)
                            {
                                XYZ pt0 = tri.get_Vertex(j % 3);
                                XYZ pt1 = tri.get_Vertex((j + 1) % 3);
                                Line li = Line.CreateBound(pt0, pt1);
                                Point pt = Point.Create(pt0);
                                //triBuilder.AddCurve(li);
                                //triBuilder.AddPoint(pt);
                            }

                            var vec01 = triPts[1] - triPts[0];
                            var vec02 = triPts[2] - triPts[0];
                            var norm = vec01.CrossProduct(vec02).Normalize();
                            var centroid= (triPts[0] + triPts[1] + triPts[2]) /3;
                            Line normDir = Line.CreateBound(centroid, centroid + 100/ 304.8 * norm);
                            triBuilder.AddCurve (normDir);
                           


                        }
                    }
                }
                DirectShape dsGp = null;
#if Revit2016
dsGp = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel), new Guid().ToString(), new Guid().ToString());
#else
                dsGp = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
#endif
                dsGp.AppendShape(triBuilder);
                t.Commit();
            }
               
            return Result.Succeeded;
        }

        private List<Mesh> GetMeshInElement(Element elem)
        {
            var view = elem.Document.ActiveView;
            GeometryElement ge=elem.get_Geometry(new Options() { View= view,ComputeReferences=true });
            List<Mesh> meshes = new List<Mesh>();   
            if(ge!=null)
            {
                foreach (var geoObj in ge)
                {
                    if(geoObj is Mesh)
                    {
                        Mesh mesh = (Mesh)geoObj;
                        meshes.Add(mesh);
                    }
                    else if(geoObj is Solid)
                    {
                        var sld = (Solid)geoObj;
                        if(sld.Faces.Size>0)
                        {
                            foreach (Face fa in sld.Faces)
                            {
                                meshes.Add(fa.Triangulate());
                            }
                        }
                    }
                    else if(geoObj is GeometryInstance)
                    {
                        meshes.AddRange(GetMeshesInGeomeetryInstance((GeometryInstance)geoObj));
                    }
                }
                
            }
            return meshes;
        }
        private List<Mesh> GetMeshesInGeomeetryInstance(GeometryInstance geoInstance)
        {
            GeometryElement ge = geoInstance.GetInstanceGeometry();
            List<Mesh> meshes = new List<Mesh>();
            if (ge != null)
            {
                foreach (var geoObj in ge)
                {
                    if (geoObj is Mesh)
                    {
                        Mesh mesh = (Mesh)geoObj;
                        meshes.Add(mesh);
                    }
                    else if (geoObj is Solid)
                    {
                        var sld = (Solid)geoObj;
                        if (sld.Faces.Size > 0)
                        {
                            foreach (Face fa in sld.Faces)
                            {
                                meshes.Add(fa.Triangulate());
                            }
                        }
                    }
                    else if(geoObj is GeometryInstance)
                    {
                        meshes.AddRange (GetMeshesInGeomeetryInstance((GeometryInstance)geoObj));
                    }
                }
            }
            return meshes;
        }
    }

}
