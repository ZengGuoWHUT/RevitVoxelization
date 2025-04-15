using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Autodesk.Revit.DB;

namespace RevitVoxelzation
{
    public class VoxelizationTool
    {
    }
    #region Geometry Primitive
    
    
    public class MeshDocument
    {
        public MeshDocument Paremt { get; set; } = null;
        public string Name { get; set; }
        public List<MeshElement> Elements { get; set; } = new List<MeshElement>();
        public List<MeshDocument> LinkDocuments { get; set; } = new List<MeshDocument>();
        
        public Transform Transform { get; set; }
        public MeshDocument(string name, List<MeshElement> elements, Transform transform)
        {
            Name = name;
            Elements = elements;
            Transform = transform;
        }
        
        public IEnumerable<MeshElement> GetAllElementsInDocumentAndLink(bool excludeSymbol)
        {
            //export elem in current model
            foreach (var elem in this.Elements)
            {
                if(excludeSymbol && elem.IsSymbol)
                {
                    continue;
                }
                else
                {
                    yield return elem;
                }
            }
            //export linked element
            foreach (var doc in this.LinkDocuments)
            {
                foreach (var elem in doc.GetAllElementsInDocumentAndLink(excludeSymbol))
                {
                    yield return elem;
                }
            }
        }

       
        
        
    }
    public class CellIndex:IEquatable<CellIndex>
    {
        public int Col { get; set; }
        public int Row { get; set; }
        public CellIndex (int col, int row)
        {
            this.Col = col;
            this.Row = row;
        }
        /*
        public static bool operator ==(CellIndex left,CellIndex right)
        {
            return left.Equals(right);

        }
        public static bool operator !=(CellIndex left, CellIndex right)
        {
            return !(left==right);
        }
        */
        private string indexString = string.Empty;
        public string IndexString
        {
            get
            {
                if(indexString == string.Empty)
                {
                    indexString = this.ToString();
                }
                return indexString;
            }
        }
        public static CellIndex operator +(CellIndex x,CellIndex y)
        {
            return new CellIndex(x.Col+y.Col, x.Row+y.Row);
        }
        public static CellIndex operator -(CellIndex x, CellIndex y)
        {
            return new CellIndex(x.Col -y.Col, x.Row - y.Row);
        }
       
        public override int GetHashCode()
        {
            return this.IndexString.GetHashCode();
            //return base.GetHashCode();
        }
       
        public override string ToString()
        {
            return String.Format("{0},{1}", this.Col, this.Row);
            //return base.ToString();
        }

        public bool Equals(CellIndex other)
        {
           
            if (other.Col == this.Col && other.Row == this.Row)
            {
                return true;
            }
            else
            {
                return false;
            }
            //throw new NotImplementedException();
        }
    }

    public class CellIndex3D : IEquatable<CellIndex3D>
    {
        public int Col { get; set; }
        public int Row { get; set; }
        public int Layer { get; set; }
        public CellIndex3D(int col, int row, int layer)
        {
            Col = col;
            Row = row;
            Layer = layer;
        }
        public bool Equals(CellIndex3D other)
        {
            return (Col == other.Col && Row == other.Row && Layer == other.Layer);
            //throw new NotImplementedException();
        }
        public override int GetHashCode()
        {
            return this.ToString().GetHashCode();
            //return base.GetHashCode();
        }
        public override string ToString()
        {
            return string.Format("{0}_{1}_{2}", Col, Row, Layer);
            // base.ToString();
        }
    }

    
    /// <summary>
    /// Raw mesh element
    /// </summary>
    public class MeshElement
    {
        public MeshDocument Document { get; set; }
        public string ElementId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; } = "Empty";
        public List<MeshSolid> Solids { get; set; } = new List<MeshSolid>();
       
        public bool IsSymbol { get; internal set; }
        public bool IsSupportElem { get; set; } = false;
        public bool IsActive { get; set; } = true;
        public bool isTransport { get; internal set; }

        public MeshElement ()
        {

        }
        public MeshElement(MeshDocument document, string elementId, List<MeshSolid> solids)
        {
            this.Document = document;
            ElementId = elementId;
            Solids = solids;
            
        }
        public double GetBoxArea()
        {
            double area = 0;
            List<Vec3> vertices = new List<Vec3>();
            foreach (var sld in this.Solids)
            {
                vertices.AddRange(sld.Vertices);
            }
            if(vertices.Count !=0)
            {
                double dblXMax = double.MinValue;
                double dblYMax = double.MinValue;
                double dblZMax = double.MinValue;
                double dblXMin = double.MaxValue;
                double dblYMin = double.MaxValue;
                double dblZMin = double.MaxValue;

                foreach (var v in vertices)
                {
                    dblXMax = Math.Max(dblXMax, v.X);
                    dblYMax = Math.Max(dblYMax, v.Y);
                    dblZMax = Math.Max(dblZMax, v.Z);
                    dblXMin = Math.Min(dblXMin, v.X);
                    dblYMin = Math.Min(dblYMin, v.Y);
                    dblZMin = Math.Min(dblZMin, v.Z);
                }
                
                var xScale = dblXMax - dblXMin;
                var yScale = dblYMax - dblYMin;
                area= xScale * yScale*2;
                
            }
            return area;
        }

        public double GetTriangleLength()
        {
            var len = 0d;
            foreach (var sld in this.GetSolids())
            {
                foreach (var tri in sld.Triangles)
                {
                    for(int i=0;i<=2;i++)
                    {
                        var v0=tri.Get_Vertex(i);
                        var v1=tri.Get_Vertex((i+1)%3);
                        Vec3 edge = v1 - v0;
                        len += edge.GetLength();
                    }
                    len += tri.Get_ProjectionArea();
                }
            }
            return len;
        }


        public void CreateSolid(List<MeshData> meshes)
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
            MeshSolid ms = new MeshSolid(this, verticesInSld, TriVerticesIndexes);
            this.Solids.Add(ms);
        }

        public int GetTriangleNumber()
        {
            int numTri = 0;
            foreach (var sld in this.Solids)
            {
                numTri += sld.Triangles.Count;
            }
            return numTri;
        }
        private Transform GetTotalTransform()
        {
            var doc = this.Document;
            Transform t = Transform.Idnentity;
            while (doc!=null)
            {
                t=(doc.Transform).Multiply (t);
                doc = doc.Paremt;
            }
            return t;
        }
        public IEnumerable<MeshSolid> GetSolids()
        {
            List<MeshSolid> solids = new List<MeshSolid>();
            var trf = GetTotalTransform();
            foreach (var sld in this.Solids)
            {
                //solids.Add(sld.CopyByTransform(trf));
                yield return (sld.CopyByTransform(trf));
            }
            
        }

        
        /// <summary>
        /// 用于为体素化准备预分配内存
        /// </summary>
        /// <param name="voxelSize">体素尺寸</param>
        public void GetVoxelCapacity(double voxelSize)
        {
            foreach (var sld in this.Solids)
            {

            }
        }
    }
    public class MeshElementCollection
    {
        /// <summary>
        /// the size(area, length) of the collection
        /// </summary>
        public double Size { get; set; }
        public List<MeshElement> Elements { get; set; }
        public List<VoxelElement> VoxelElement { get; set; }
        public MeshElementCollection(double size, List<MeshElement> elements)
        {
            this.Size = size;
            this.Elements = elements;
        }
        /// <summary>
        /// generate elements with grid points but no voxels;
        /// </summary>
        /// <param name="voxelSize"></param>
        /// <returns></returns>
        public void InitializeVoxelElement(double voxelSize)
        {
            this.VoxelElement = new List<VoxelElement> ();
            foreach (var elem in this.Elements)
            {
                var ve = new VoxelElement(elem, voxelSize);
                this.VoxelElement.Add (ve);
            }
            
        }
    }
   

    public class MeshSolid:IGridPointHost
    {
        public MeshElement Owner { get; set; }
        public List<MeshTriangle> Triangles { get; set; }
        public List<Vec3> Vertices { get; set; }
        public  List<MeshEdge> Edges { get; set; }
        public  int ColLocal { get; set; }
        public  int RowLocal { get; set; }
        public List<GridPoint> GridPoints { get; set; }

        public MeshSolid()
        {

        }
        public MeshSolid(MeshElement owner, List<Vec3> vertices, List<int> triangles)
        {
            this.Owner = owner;
            this.Vertices = vertices;
            this.Triangles = new List<MeshTriangle>();
            this.GridPoints = new List<GridPoint>();
            //Create an array for creating edges
            this.Triangles = new List<MeshTriangle>();
            List<MeshEdge> meshEdges = new List<MeshEdge>();
            //use an array to store the edge index
            //arrEdge[i][j]stores the index of edgeij
            Dictionary<int,Dictionary<int,int>> arrEdge = new Dictionary<int, Dictionary<int, int>>();
            for (int i=0;i<=triangles.Count -1;i+=3)
            {
                int triIndex=(int)Math.Floor((double)i/3);
                int vi0=triangles[i];
                int vi1 = triangles[i + 1];
                int vi2= triangles[i + 2];
                int[] visInCurTri=new int[3] { vi0,vi1,vi2};
                //create triangles
                MeshTriangle tri = new MeshTriangle(this, visInCurTri);
                this.Triangles.Add(tri);
                //create 3 edges
                /*
                int[] edgeIndex = new int[3];
                tri.EdgeIndex = edgeIndex;
                for(int j=0;j<=2;j++)
                {
                    var vst=visInCurTri[j%3];
                    var ved=visInCurTri[(j+1)%3];
                    int vMin = Math.Min(vst, ved);
                    int vMax = Math.Max(vst, ved);
                    MeshEdge edge = new MeshEdge(this, vst, ved, new List<GridPoint>());
                    string strEdge = edge.ToString();
                    int edgeIdx = meshEdges.Count;
                    if (!arrEdge.ContainsKey (vMin))
                    {
                        arrEdge.Add(vMin, new Dictionary<int, int>());
                    }
                    var edgeEndPt = arrEdge[vMin];
                    if (!edgeEndPt.ContainsKey(vMax)) //the edge has not been added yet
                    {
                        edgeEndPt.Add (vMax,edgeIdx);
                        meshEdges.Add(edge);
                    }
                    else //the edge exist
                    {
                        edgeIdx = edgeEndPt[vMax];
                    }
                    edgeIndex[j] = edgeIdx;
                }
                */
            }
            this.Edges = meshEdges;
        }

        public void GetBoundingBox(out Vec3 min,out Vec3 max)
        {
            min = Vec3.Zero;
            max=Vec3.Zero;
            if (this.Vertices.Count != 0)
            {
                double dblXMax = double.MinValue;
                double dblYMax = double.MinValue;
                double dblZMax = double.MinValue;
                double dblXMin = double.MaxValue;
                double dblYMin = double.MaxValue;
                double dblZMin = double.MaxValue;
                foreach (var v in this.Vertices)
                {
                    dblXMax = Math.Max(dblXMax, v.X);
                    dblYMax = Math.Max(dblYMax, v.Y);
                    dblZMax = Math.Max(dblZMax, v.Z);
                    dblXMin = Math.Min(dblXMin, v.X);
                    dblYMin = Math.Min(dblYMin, v.Y);
                    dblZMin = Math.Min(dblZMin, v.Z);
                }
                min = new Vec3(dblXMin, dblYMin, dblZMin);
                max = new Vec3(dblXMax, dblYMax, dblZMax);
            }
            
        }

        public void GenerateGridPoints(Vec3 origin, double voxelSize)
        {
            this.GridPoints = new List<GridPoint>();
            foreach (var v in this.Vertices)
            {
                var v2Origin = (v - origin) / voxelSize;
                double colRnd = Math.Round (v2Origin.X,3);
                double rowRnd =Math.Round (v2Origin.Y,3);
                int col =(int) Math.Floor(colRnd);
                int row =(int) Math.Floor(rowRnd);
                GridPointType gpt = GridPointType.VGP;
                if(col==colRnd && row!=rowRnd) //CGP
                {
                    gpt = GridPointType.CGP;
                }
                else if(col!=colRnd && row==rowRnd)//rgp
                {
                    gpt = GridPointType.RGP;
                }
                else if(col==colRnd && row==rowRnd) //igp
                {
                    gpt = GridPointType.IGP;
                }
                GridPoint gp = new GridPoint(v.X,v.Y, col, row, v.Z, gpt);
                //result.Add(gp);
                this.GridPoints.Add(gp);
            }
           
        }
        public void SetAllGridPoints(Vec3 origin, double voxelSize,out int gpNumber)
        {
            gpNumber = 0;
            if(this.GridPoints ==null || this.GridPoints.Count == 0)
            {
                GenerateGridPoints(origin, voxelSize);
                gpNumber += this.GridPoints.Count;
            }
           //Triangle
           
           foreach (var tri in this.Triangles)
           {
               tri.GenerateGridPoints(origin, voxelSize);
               gpNumber += tri.GridPoints.Count;
           }
           
        }

        public IEnumerable<GridPoint> GetAllGridPoints()
        {
            foreach (var tri in this.Triangles)
            {
                foreach (var gp in tri.GridPoints)
                {
                    yield return gp;
                }
            }

        }

        public IEnumerable<Voxel> GenerateVoxels(bool fillAfterMerge)
        {
            //get voxel range
            GetSolidVoxelRange(out int colMin, out int rowMin, out int colMax, out int rowMax);
            int colRng = colMax - colMin + 1;
            int rowRng=rowMax - rowMin + 1;
            List<Voxel>[,] voxInSld= new List<Voxel>[colRng, rowRng];
            foreach(var tri in this.Triangles)
            {
                foreach(var voxel in  tri.GenerateVoxels())
                {
                    var colLoc = voxel.ColIndex-colMin;
                    var rowLoc = voxel.RowIndex-rowMin;
                    if (voxInSld[colLoc, rowLoc] == null)
                        voxInSld[colLoc, rowLoc] = new List<Voxel>();
                    voxInSld[colLoc, rowLoc].Add(voxel);
                }
            }
            //merge voxels
            if(fillAfterMerge)
            {
                MergeIntersectedVoxels(ref voxInSld);
                //Get adjacen relationship of voxels
                FindAdjacentVoxels(ref voxInSld, out List<Voxel> boundaryVoxels);
                //fill voxels
                FillVoxels(ref voxInSld, boundaryVoxels, fillAfterMerge);
            }
            //remove redundancy link info
            foreach(var voxels in voxInSld)
            {
                if(voxels!=null)
                {
                    foreach (var vox in voxels)
                    {
                        yield return vox;
                    }
                }
            }
        }

        public IEnumerable<Voxel> GenerateVoxels(bool mergeIntersecting,  bool fillAfterMerge)
        {
            //get voxel range
            GetSolidVoxelRange(out int colMin, out int rowMin, out int colMax, out int rowMax);
            int colRng = colMax - colMin + 1;
            int rowRng = rowMax - rowMin + 1;
            List<Voxel>[,] voxInSld = new List<Voxel>[colRng, rowRng];
            foreach (var tri in this.Triangles)
            {
                foreach (var voxel in tri.GenerateVoxels())
                {
                    var colLoc = voxel.ColIndex - colMin;
                    var rowLoc = voxel.RowIndex - rowMin;
                    if (voxInSld[colLoc, rowLoc] == null)
                        voxInSld[colLoc, rowLoc] = new List<Voxel>();
                    voxInSld[colLoc, rowLoc].Add(voxel);
                }

            }
            //merge voxels
            if (mergeIntersecting)
            {
                MergeIntersectedVoxels(ref voxInSld);
                if (fillAfterMerge)
                {
                    //Get adjacen relationship of voxels
                    FindAdjacentVoxels(ref voxInSld, out List<Voxel> boundaryVoxels);
                    //fill voxels
                    FillVoxels(ref voxInSld, boundaryVoxels, fillAfterMerge);
                }
            }
           
            
            //remove redundancy link info
            foreach (var voxels in voxInSld)
            {
                if (voxels != null)
                {
                    foreach (var vox in voxels)
                    {
                        yield return vox;
                    }
                }
            }
        }
        private List<Voxel>[,] voxInSld;
        int colMin;
        int rowMin;
        public int GenerateGridPoints_Parallel(Vec3 origin,double voxelSize)
        {
            int numPts = 0;
            //generate grid points
            this.GenerateGridPoints(origin,voxelSize);
            foreach (var tri in this.Triangles)
            {
                tri.GenerateGridPoints_Parallel(origin,voxelSize);
                numPts += tri.GridPoints.Count;
            }
            GetSolidVoxelRange(out  colMin, out rowMin, out int colMax, out int rowMax);
            int colRng = colMax - colMin + 1;
            int rowRng = rowMax - rowMin + 1;
            voxInSld = new List<Voxel>[colRng, rowRng];
            return numPts;
        }
        public IEnumerable<Voxel> GenerateVoxels_Parallel(bool fillAfterMerge)
        {
            foreach (var tri in this.Triangles)
            {
                foreach (var voxel in tri.GenerateVoxels())
                {
                    var colLoc = voxel.ColIndex - colMin;
                    var rowLoc = voxel.RowIndex - rowMin;
                    if (voxInSld[colLoc, rowLoc] == null)
                        voxInSld[colLoc, rowLoc] = new List<Voxel>();
                    voxInSld[colLoc, rowLoc].Add(voxel);
                }
            }
            //merge voxels
            MergeIntersectedVoxels(ref voxInSld);
            //Get adjacen relationship of voxels
            FindAdjacentVoxels(ref voxInSld, out List<Voxel> boundaryVoxels);
            //fill voxels
            FillVoxels(ref voxInSld, boundaryVoxels, fillAfterMerge);
            //remove redundancy link info
            foreach (var voxels in voxInSld)
            {
                if (voxels != null)
                {
                    foreach (var vox in voxels)
                    {
                        yield return vox;
                    }
                }
            }
        }



        private void MergeIntersectedVoxels(ref List<Voxel>[,] voxel2Merge)
        {
            var colRng = voxel2Merge.GetUpperBound(0) + 1;
            var rowRng = voxel2Merge.GetUpperBound(1) + 1;
            for (int colLoc = 0; colLoc < colRng; colLoc++)
            {
                for (int rowLoc = 0; rowLoc < rowRng; rowLoc++)
                {
                    var voxelOriginal = voxel2Merge[colLoc, rowLoc];
                    if (voxelOriginal != null)
                    {
                        var mergedVoxel = new List<Voxel>() { Capacity=voxelOriginal.Count};
                        //sort voxelOriginal by bottom and collect voxels from col/row planes
                        //var voxes2Merge = SortVoxelsByBtmElevAndExcludeNonCommonVoxels(voxelOriginal, out var voxOdd);
                        List<Voxel> voxOdd = voxelOriginal.Where(c => c.VoxType ==VoxelType.Odd).ToList();
                        if(voxOdd .Count ==voxelOriginal.Count ) //only odd voxel exist
                        {
                            mergedVoxel = MergeVoxelColumn(voxOdd, true);
                        }
                        else // odd and merged voxels all exist
                        {
                            mergedVoxel= MergeVoxelColumn(voxelOriginal, true);
                        }
                        voxel2Merge[colLoc, rowLoc] = mergedVoxel;
                    }
                }
            }
        }
        private List<Voxel> MergeVoxelColumn(List<Voxel> voxelOriginal,bool needSort)
        {
            if(voxelOriginal.Count ==0)
                return voxelOriginal;
            List<Voxel> mergedVoxels = new List<Voxel>() { Capacity =voxelOriginal.Count};
            var voxes2Merge = voxelOriginal;
            if(needSort)
            {
                voxes2Merge = voxelOriginal.OrderBy(c => c.BottomElevation).ToList();
            }
            int snakePointer = 0;
            int foodPointer = 1;
            var snakeVoxel = voxes2Merge[snakePointer];
            mergedVoxels.Add(snakeVoxel);
            while (foodPointer < voxes2Merge.Count)
            {
                Voxel foodVoxel = voxes2Merge[foodPointer];
                //check if snake voxels intersects food voxels
                if (Math.Round(snakeVoxel.BottomElevation - foodVoxel.TopElevation, 4) <= 0 &&
                    Math.Round(snakeVoxel.TopElevation - foodVoxel.BottomElevation, 4) >= 0)
                {
                    snakeVoxel.TopElevation = Math.Max(snakeVoxel.TopElevation, foodVoxel.TopElevation);
                    //if the snaek voxel is odd and the food is common, modify the type of the voxel as common
                    if(snakeVoxel.VoxType ==VoxelType.Odd && foodVoxel.VoxType ==VoxelType.Common)
                    {
                        snakeVoxel.VoxType = VoxelType.Common;
                    }
                    foodPointer += 1;
                }
                else //snake voxel and food voxel do not intersect
                {
                    snakePointer = foodPointer;
                    snakeVoxel = voxes2Merge[snakePointer];
                    mergedVoxels.Add(snakeVoxel);
                    foodPointer += 1;
                }
            }
            //create top-btm relationship
            for (int i = 0; i <= mergedVoxels.Count - 2; i++)
            {
                var voxBelow = mergedVoxels[i];
                var voxAbove = mergedVoxels[i + 1];
                voxBelow.TopVoxel = voxAbove;
                voxAbove.BottomVoxel = voxBelow;
            }
            return mergedVoxels;
        }

        private void ReLinkLowerUpperVoxels(List<Voxel> baseVoxels,List<Voxel> voxels2Insert)
        {
            foreach(var v0 in baseVoxels)
            {
                var v0_btm = Math.Round(v0.BottomElevation, 4);
                var v0_Top = Math.Round(v0.TopElevation, 4);
                double dblElevLower = double.MinValue;
                if(v0.BottomVoxel!=null)
                {
                    dblElevLower = v0.BottomVoxel.TopElevation;
                }
                double dblElevUpper = double.MaxValue;
                if(v0.TopVoxel!=null)
                {
                    dblElevUpper = v0.TopVoxel.BottomElevation;
                }
                foreach (var v1 in voxels2Insert)
                {
                    var v1_btm = Math.Round(v1.BottomElevation, 4);
                    var v1_top = Math.Round(v1.TopElevation, 4);
                    if(v1_top<v0_btm) //v1 is under v0
                    {
                        if(v1.TopElevation >dblElevLower)
                        {
                            dblElevLower = v1.TopElevation;
                            v0.BottomVoxel = v1;

                        }
                    }
                    else if(v1_btm >v0_Top) //v1 is above v0
                    {
                        if(v1.BottomElevation<=dblElevUpper)
                        {
                            dblElevUpper = v1.BottomElevation;
                            v0.TopVoxel = v1;
                        }
                    }
                    else // the 2 voxels intersects
                    {
                        if(v1_top>=v0_Top && v1_btm<v0_Top) //top inteserct
                        {
                            dblElevUpper = 0;
                            v0.TopVoxel = v1;
                            /*
                            if(v1 is OddVoxel)
                            {
                                var oddVox = v1 as OddVoxel;
                               
                                oddVox.TopInnerVoxels.Add (v0);
                            }
                            */
                        }
                        else if(v1_btm <=v0_btm && v1_top >v0_btm) //bottom interesct
                        {
                            dblElevLower = 0;
                            v0.BottomVoxel = v1;
                            /*
                            if (v1 is OddVoxel)
                            {
                                var oddVox = v1 as OddVoxel;
                                oddVox.BottomInnerVoxels.Add(v0);
                            }
                            */
                        }
                        else // the voxel is completely inside the odd voxel
                        {
                            dblElevLower = 0;
                            v0.BottomVoxel = v1;
                            dblElevUpper = 0;
                            v0.TopVoxel = v1;
                            /*
                            if (v1 is OddVoxel)
                            {
                                var oddVox = v1 as OddVoxel;
                                oddVox.BottomInnerVoxels.Add(v0);
                                oddVox.TopInnerVoxels.Add(v0);
                            }
                            */
                        }
                    }
                }
            }
        }
        private List<Voxel> SortVoxelsByBtmElevAndExcludeNonCommonVoxels(List<Voxel> vox2Sort,out List<Voxel> voxUnCommon)
        {
            List<Voxel> result = new List<Voxel>();
            voxUnCommon = new List<Voxel>();
            foreach (var vox in vox2Sort)
            {
                if(!(vox.VoxType ==VoxelType.Odd)) //the voxel is a common voxel
                {
                    result.Add(vox);
                }
                else
                {
                    voxUnCommon.Add(vox);
                }
            }
            return result.OrderBy(c => c.BottomElevation).ToList() ;
        }
        private void FindAdjacentVoxels(ref List<Voxel>[,] mergedVoxels,out List<Voxel> BoundaryVoxels)
        {
            var colRng = mergedVoxels.GetUpperBound(0) + 1;
            var rowRng = mergedVoxels.GetUpperBound(1) + 1;
            int i = 0;
            for (int colLoc = 0; colLoc < colRng; colLoc++)
            {
                for (int rowLoc = 0; rowLoc < rowRng; rowLoc++)
                {
                    var voxels=mergedVoxels[colLoc, rowLoc];
                    if(voxels!=null)
                    {
                        foreach (var vox in voxels)
                        {
                            vox.Index = i;
                            vox.BottomActivater = -1;
                            vox.TopActivater = -1;
                            vox.BoundaryActivater = -1;
                            var voxGapRangeLower = vox.GetLowerGapRange();
                            var voxGapRangeUpper = vox.GetUpperGapRange();
                            //get offset
                            Tuple<int, int>[] offsets = new Tuple<int, int>[4] { new Tuple<int, int>(1, 0), new Tuple<int, int>(0, 1) , new Tuple<int, int>(-1, 0), new Tuple<int, int>(0, -1) };
                            for(int adjSeq=0;adjSeq<4;adjSeq++)
                            {
                                if (vox.BottomAdjVoxels[adjSeq] != null)// the voxel has been scanned
                                    continue;
                                var offset = offsets[adjSeq];
                                var colAdj = colLoc + offset.Item1;
                                var rowAdj = rowLoc + offset.Item2;
                                if(colAdj >=0 &&  colAdj <=colRng-1 && rowAdj>=0 && rowAdj <=rowRng -1)
                                {
                                    var voxesAdj = mergedVoxels[colAdj,rowAdj];
                                    if(voxesAdj!=null)
                                    {
                                        //first, find voxels vertically intersects with current voxel
                                        List<Voxel> voxelsSideNearby = new List<Voxel>() { Capacity=voxesAdj.Count};
                                        //List<Voxel> voxelsNearby = voxesAdj; 
                                        foreach (var voxAdj in voxesAdj)
                                        {
                                            var voxAdjBtm = voxAdj.BottomElevation;
                                            if(Math.Round (voxAdjBtm-vox.TopElevation,4)<=0 
                                                &&  Math.Round(vox.BottomElevation-voxAdj.TopElevation,4)<=0)// the 2 voxels intersects
                                            {
                                                voxelsSideNearby.Add(voxAdj);
                                            }
                                        }
                                        //search the voxelNear for voxel
                                        //search bottom
                                        double minBtm = double.MaxValue;
                                        Voxel voxUnderNear = null;
                                        double maxTop = double.MinValue;
                                        Voxel voxUpperNear = null;
                                       
                                        if(voxelsSideNearby .Count ==0)//no voxels side adjacent to current voxel
                                        {
                                           vox.IsBoundaryVoxel = true;
                                        }
                                        else
                                        {
                                            //get the top-bottom linking relationship
                                            foreach (var voxNearBy in voxelsSideNearby)
                                            {
                                                var nearBtmAccessibleRng = voxNearBy.GetLowerGapRange();
                                                var nearTopAccessibleRng = voxNearBy.GetUpperGapRange();
                                                if (nearBtmAccessibleRng.Intersect(voxGapRangeLower)) //the adj voxels can be visited
                                                {
                                                    if (voxNearBy.BottomElevation < minBtm)
                                                    {
                                                        minBtm = voxNearBy.BottomElevation;
                                                        voxUnderNear = voxNearBy;
                                                    }
                                                }
                                                if (nearTopAccessibleRng.Intersect(voxGapRangeUpper))
                                                {
                                                    if (voxNearBy.TopElevation > maxTop)
                                                    {
                                                        maxTop = voxNearBy.TopElevation;
                                                        voxUpperNear = voxNearBy;
                                                    }
                                                }
                                            }
                                            //add nearby info
                                            //the Vox.Bottom/TopAdjVoxels property uses indexes to illustrate the directions of
                                            //the nearby voxels;
                                            //0-right;1-up;2-left;3-down
                                            if (voxUnderNear != null)
                                            {
                                                vox.BottomAdjVoxels[adjSeq] = voxUnderNear;
                                                voxUnderNear.BottomAdjVoxels[(adjSeq + 2)%4] = vox;
                                            }
                                            if (voxUpperNear != null)
                                            {
                                                
                                                vox.TopAdjVoxels[adjSeq] = voxUpperNear;
                                                voxUpperNear.TopAdjVoxels[(adjSeq + 2)%4] = vox;
                                            }
                                        }
                                    }
                                    else//the voxel is at boundary
                                    {
                                        vox.IsBoundaryVoxel = true;
                                    }
                                }
                                else // the voxel is at the boundary
                                {
                                    vox.IsBoundaryVoxel = true;
                                }
                            }
                            i += 1;
                        }
                    }
                }
            }
            //find boundary voxels
            BoundaryVoxels = new List<Voxel>();
            foreach (var voxCol in mergedVoxels)
            {
                if(voxCol !=null)
                {
                    var firstVox = voxCol.FirstOrDefault();
                    if(firstVox.IsBoundaryVoxel)
                    {
                        BoundaryVoxels.Add(firstVox);
                    }
                }
            }
            BoundaryVoxels.TrimExcess();
        }

       
        private void FillVoxels(ref List<Voxel>[,] mergedVoxels, List<Voxel> boundaryVoxels,bool FillAfterMark)
        {
            //Create a stack stkVoxelBtm And a stack stkVoxTop
            Stack<Voxel> stkVoxOuter = new Stack<Voxel>();
            
            //Push the boundary voxels in stkBdryVoxel and stkVoxTop
            foreach (var bdryVox in boundaryVoxels)
            {
                //for debug only-remove after release
                bdryVox.BoundaryActivater = bdryVox.Index;
                bdryVox.TopOutside = true;
                bdryVox.BottomOutside = true;
                stkVoxOuter.Push(bdryVox);
            }
            while (stkVoxOuter.Count != 0)
            {
                var voxCur = stkVoxOuter.Pop();
                //if there is a potential boundary voxels under voxBtmNear
                //conver it to be a real bounary voxels and add it to stkVoxBtm and stVoxTop
                if(voxCur.BottomOutside==true)
                {
                    if (voxCur.BottomVoxel != null)
                    {
                        var voxUnder = voxCur.BottomVoxel;
                        //check if the vox has been scanned, if not, push it in stacks
                        if(voxUnder.IsBoundaryVoxel)
                        {
                            if (voxUnder.TopOutside == false || voxUnder.BottomOutside ==false)
                            {
                                if(voxUnder.TopOutside ==false)
                                {
                                    voxUnder.TopOutside = true;
                                    voxUnder.TopActivater = voxCur.Index;
                                    voxUnder.BoundaryActivater = voxCur.BoundaryActivater;
                                }
                                if(voxUnder.BottomOutside ==false)
                                {
                                    voxUnder.BoundaryActivater = voxCur.BoundaryActivater;
                                    voxUnder.BottomActivater = voxCur.Index;
                                    voxUnder.BottomOutside = true;
                                }
                                stkVoxOuter.Push(voxUnder);
                            }
                        }
                    }
                    //scan voxels near voxCur
                    for (int dir = 0; dir <= 3; dir++)
                    {
                        var voxBtmNear = voxCur.BottomAdjVoxels[dir];
                        if (voxBtmNear != null && voxBtmNear.BottomOutside == false)
                        {
                            //debug only
                            voxBtmNear.BottomActivater = voxCur.Index;
                            voxBtmNear.BoundaryActivater = voxCur.BoundaryActivater;
                            //end debug
                            if(voxBtmNear.BottomOutside ==false)
                            {
                                voxBtmNear.BottomOutside = true;
                                stkVoxOuter.Push(voxBtmNear);
                            }
                        }
                    }
                }
                
                if(voxCur .TopOutside ==true)
                {
                    
                    var voxAbove = voxCur.TopVoxel;
                    //if there are potential to voxels above voxTopNear, 
                    //convert it to be a real boundary voxels
                    if (voxAbove != null && voxAbove.IsBoundaryVoxel)
                    {
                        if (voxAbove.BottomOutside == false || voxAbove .TopOutside ==false)
                        {
                            if(voxAbove.BottomOutside ==false)
                            {
                                voxAbove.BottomActivater = voxCur.Index;
                                voxAbove.BoundaryActivater = voxCur.BoundaryActivater;
                                voxAbove.BottomOutside = true;
                            }
                            if(voxAbove .TopOutside ==false)
                            {
                                voxAbove.BottomActivater = voxCur.Index;
                                voxAbove.BoundaryActivater = voxCur.BoundaryActivater;
                                voxAbove.TopOutside = true;
                            }
                            stkVoxOuter.Push(voxAbove);
                        } 
                    }
                    //scan near
                    for (int dir = 0; dir <= 3; dir++)
                    {
                        var voxTopNear = voxCur.TopAdjVoxels[dir];
                        if (voxTopNear != null && voxTopNear.TopOutside == false)
                        {
                            //debug only
                            voxTopNear.TopActivater = voxCur.Index;
                            voxTopNear.BoundaryActivater = voxCur.BoundaryActivater;
                            //end debug
                            if(voxTopNear.TopOutside==false)
                            {
                                voxTopNear.TopOutside = true;
                                stkVoxOuter.Push(voxTopNear);
                            }
                        }
                    }
                }
            }
            
            //get valid voxels
            //a valid voxel is a common voxel
            var colRng = mergedVoxels.GetUpperBound(0) + 1;
            var rowRng = mergedVoxels.GetUpperBound(1) + 1;
            for (int colLoc = 0; colLoc < colRng; colLoc++)
            {
                for (int rowLoc = 0; rowLoc < rowRng; rowLoc++)
                {
                    var voxels = mergedVoxels[colLoc, rowLoc];
                    if (voxels != null)
                    {
                        List<Voxel> validVoxels = new List<Voxel>();
                        //debug

                        foreach (var vox in voxels)
                        {
                            if (!(vox.VoxType == VoxelType.Odd))
                            {
                                validVoxels.Add (vox);
                            }
                            else //check if side voxel is outside
                            {
                                //debug
                                if(!vox.IsBoundaryVoxel)
                                {
                                    validVoxels.Add(vox);
                                }
                            }
                        }
                        mergedVoxels[colLoc, rowLoc] =( validVoxels.Count ==0?null:validVoxels );
                    }
                }
            }
            //fill the voxels
            if(FillAfterMark)
            {
                for (int colLoc = 0; colLoc < colRng; colLoc++)
                {
                    for (int rowLoc = 0; rowLoc < rowRng; rowLoc++)
                    {
                        var voxels = mergedVoxels[colLoc, rowLoc];
                        if (voxels != null)
                        {
                            var snakeVoxel = voxels[0];
                            var foodVoxel = snakeVoxel.TopVoxel;
                            List<Voxel> snakeVoxels = new List<Voxel>();
                            snakeVoxels.Add(snakeVoxel);
                            while (foodVoxel != null)
                            {
                                if (snakeVoxel.TopOutside && foodVoxel.BottomOutside)
                                {
                                    snakeVoxel = foodVoxel;
                                    snakeVoxels.Add(snakeVoxel);
                                    foodVoxel = snakeVoxel.TopVoxel;
                                }
                                else //snake voxel merge food voxel
                                {
                                    snakeVoxel.TopElevation = foodVoxel.TopElevation;
                                    snakeVoxel.TopVoxel = foodVoxel.TopVoxel;
                                    snakeVoxel.TopOutside = foodVoxel.TopOutside;
                                    foodVoxel.Parent = snakeVoxel;
                                    foodVoxel = snakeVoxel.TopVoxel;
                                }
                            }
                            mergedVoxels[colLoc, rowLoc] = snakeVoxels;
                        }
                    }
                }
            }
        }
        
        private void GetSolidVoxelRange(out int colMin, out int rowMin, out int colMax, out int rowMax)
        {
            colMin = int.MaxValue;
            rowMin = int.MaxValue;
            colMax = int.MinValue;
            rowMax = int.MinValue;
            foreach (var gp in this.GridPoints)
            {
                var colMaxTemp = gp.Column;
                var rowMaxTemp = gp.Row;
                var colMinTemp = gp.Column;
                var rowMinTemp = gp.Row;
                if (gp.GridType == GridPointType.CGP)
                {
                    colMinTemp = colMinTemp - 1;
                }
                else if (gp.GridType == GridPointType.RGP)
                {
                    rowMinTemp = rowMinTemp - 1;
                }
                else if (gp.GridType == GridPointType.IGP)
                {
                    colMinTemp = colMinTemp - 1;
                    rowMinTemp = rowMinTemp - 1;
                }
                colMin = Math.Min(colMin, colMinTemp);
                rowMin = Math.Min(rowMin, rowMinTemp);
                colMax = Math.Max(colMax, colMaxTemp);
                rowMax = Math.Max(rowMax, rowMaxTemp);
            }

        }

        public void TransformSolid(Transform trf)
        {
            this.Vertices = trf.OfPoints(this.Vertices);
        }
        public MeshSolid CopyByTransform(Transform trf)
        {
            var solidCopy= new MeshSolid()
            {
                Owner = this.Owner,
                Vertices = trf.OfPoints(this.Vertices)
            };
            List<MeshEdge> edges = new List<MeshEdge>();
            foreach (var edge in this.Edges)
            {
                MeshEdge newEdge = new MeshEdge(solidCopy, edge.Vertex0, edge.Vertex1, new List<GridPoint>());
                edges.Add(newEdge);
            }
            List<MeshTriangle> meshTriangles = new List<MeshTriangle>();
            foreach (var tri in this.Triangles)
            {
                MeshTriangle newTri = new MeshTriangle(solidCopy, tri.VerticesIndex) { EdgeIndex =tri.EdgeIndex};
                meshTriangles.Add(newTri);
            }
            solidCopy.Edges = edges;
            solidCopy.Triangles = meshTriangles;
            return solidCopy;
        }
    }

    public class MeshEdge : IGridPointHost,IEquatable<MeshEdge>
    {
        public MeshSolid Owner { get; }
        public int Vertex0 { get; }
        public int Vertex1 { get; }
        public int Index { get; set; }

        public MeshEdge(MeshSolid owner, int vertex0, int vertex1, List<GridPoint> voxels)
        {
            Owner = owner;
            Vertex0 = vertex0;
            Vertex1 = vertex1;
            GridPoints = voxels;
        }
        public GridPoint[] Get_EndGridPoints()
        {
            var gp0 = this.Owner.GridPoints[Vertex0];
            var gp1= this.Owner.GridPoints[Vertex1];
            return new GridPoint[2] { gp0, gp1 };
        }
        public List<GridPoint> GridPoints { get ; set; }
        public Vec3 Get_EndPoint(int voxParam)
        {
            if(voxParam == 0)
            {
                return this.Owner.Vertices[Vertex0];
            }
            else if (voxParam == 1)
            {
                return this.Owner.Vertices[Vertex1];
            }
            else
            {
                throw new Exception("The input of edge vertices can only be 0 or 1");
            }
        }
        public override string ToString()
        {
            return String.Format("{0}-{1}", Math.Min(Vertex0, Vertex1), Math.Max(Vertex0, Vertex1));
        }
        public void GenerateGridPoints(Vec3 origin, double voxelSize)
        {
            
            this.GridPoints = new List<GridPoint>();
            //Generate GP on Edges
            //Get the edge vertices (v0,v1)
            var v0 = Get_EndPoint(0);
            var v1 = Get_EndPoint(1);
            var edgeGPEd0=this.Owner.GridPoints[Vertex0];
            var edgeGPEd1=this.Owner.GridPoints[Vertex1];

            //Get the delta of X,Y and Z
            var xDelta = v1.X - v0.X;
            var yDelta = v1.Y - v0.Y;
            var zDelta = v1.Z - v0.Z;
            
           
            int colMin = Math.Min(edgeGPEd0.Column, edgeGPEd1.Column);
            int colMax = Math.Max(edgeGPEd0.Column, edgeGPEd1.Column);
            int rowMin = Math.Min(edgeGPEd0.Row, edgeGPEd1.Row);
            int rowMax = Math.Max(edgeGPEd0.Row, edgeGPEd1.Row);

            int colSt = colMin+1;
            int colEd = colMax;
            int rowSt =rowMin+1 ; 
            int rowEd = rowMax;

            if (colMin!=colMax)
            {
                if (colMax ==edgeGPEd1.Column)
                {
                    //modify edge
                    if (edgeGPEd1.GridType == GridPointType.CGP || edgeGPEd1.GridType == GridPointType.IGP)
                    {
                        colEd -= 1;
                    }
                }
                else
                {
                    //modify colRange
                    if (edgeGPEd0.GridType == GridPointType.CGP || edgeGPEd0.GridType == GridPointType.IGP)
                    {
                        colEd -= 1;
                    }
                }
                //Get param
                double paramZX = zDelta / xDelta;
                double paramYX = yDelta / xDelta;
                double paramXY = 1 / paramYX;
                //Calculate col and row range
                //var colSt = (int)Math.Floor((vst.X - origin.X) / voxelSize) + 1;
                //var colEd = (int)Math.Ceiling((ved.X - origin.X) / voxelSize) - 1;
                //Generate GPs
                //Generate potential CGP or IGPs
                for (int col = colSt; col <= colEd; col++)
                {
                    double x = origin.X + col * voxelSize;
                    double y = v0.Y + (x - v0.X) * paramYX;
                    double z = v0.Z + (x - v0.X) * paramZX;
                    //chech if y is on the row plane
                    double dblY =Math.Round ( (y - origin.Y)/voxelSize,3);
                    int row = (int)Math.Floor(dblY);
                    if(row>=rowMin && row<=rowMax)
                    {
                        GridPointType gpt = GridPointType.CGP;
                        if (row == dblY) //IGP
                        {
                            gpt = GridPointType.IGP;
                        }
                        GridPoint gp = new GridPoint(x,y,col, row, z, gpt);
                        this.GridPoints.Add(gp);
                    }
                    else
                    {

                    }
                }
                //Gereate potential RGPs
                //get rowRange
                if (rowMax ==edgeGPEd1.Row)
                {
                    //modify rowRange
                    if (edgeGPEd1.GridType == GridPointType.RGP || edgeGPEd1.GridType == GridPointType.IGP)
                    {
                        rowEd -= 1;
                    }
                }
                else
                {
                    //modify rowRange
                    if (edgeGPEd0.GridType == GridPointType.RGP || edgeGPEd0.GridType == GridPointType.IGP)
                    {
                        rowEd -= 1;
                    }
                }
                //else rowMax=rowMin,only IGP or CGP exist, do nothing because the iterantion 
                //below will not run
                for (int row = rowSt; row <= rowEd; row++)
                {
                    double y = origin.Y + row * voxelSize;
                    double x = v0.X + (y - v0.Y) * paramXY;
                    double z = v0.Z + (x - v0.X) * paramZX;
                    double dblX =Math.Round( (x - origin.X) / voxelSize,3);
                    int col = (int)Math.Floor(dblX);
                    if(col>=colMin && col<=colMax)
                    {
                        GridPointType gpt = GridPointType.RGP;
                        //chech if y is on the row plane
                        if (col == dblX) //IGP, igonre it because it has been added when scanning cols
                        {
                            continue;
                        }
                        GridPoint gp = new GridPoint(x,y,col, row, z, gpt);
                        this.GridPoints.Add(gp);
                    }
                    else
                    {

                    }
                }
            }
            else if (rowMin  != rowMax)//the edge is in the colPlane
            {
                if (rowMax==edgeGPEd1.Row)
                {
                    //modify rowRange
                    if (edgeGPEd1.GridType == GridPointType.RGP || edgeGPEd1.GridType == GridPointType.IGP)
                    {
                        rowEd -= 1;
                    }
                }
                else if (rowMax ==edgeGPEd0.Row)
                {
                    //modify rowRange
                    if (edgeGPEd0.GridType == GridPointType.RGP || edgeGPEd0.GridType == GridPointType.IGP)
                    {
                        rowEd -= 1;
                    }
                }
                double x = v0.X;
                double paramZY = zDelta / yDelta;
                //Gereate potential RGPs
                for (int row = rowSt; row <= rowEd; row++)
                {
                    double y = origin.Y + row * voxelSize;
                    double z = v0.Z + (y - v0.Y) * paramZY;
                    double dblX =  Math.Round((x - origin.X) / voxelSize, 3);
                    int col = (int)Math.Floor(dblX);
                    if(col>=colMin && col<=colMax)
                    {
                        GridPointType gpt = GridPointType.RGP;
                        //chech if y is on the row plane
                        if (col == dblX) //IGP
                        {
                            gpt = GridPointType.IGP;
                        }
                        GridPoint gp = new GridPoint(x,y,col, row, z, gpt);
                        this.GridPoints.Add(gp);
                    }
                    else
                    {

                    }
                }
            }
            else //the edge is an vertical one,retrun
            {
                return;
            }
        }
        public bool Equals(MeshEdge other)
        {
            return this.ToString() == other.ToString();
            //throw new NotImplementedException();
        }
        public override int GetHashCode()
        {
            return this.ToString().GetHashCode();
            //return base.GetHashCode();
        }
    }
    
    public enum TriangleType
    {
        Common=0,
        ColOverlap=1,
        RowOverlap=2,
    }
    public class MeshTriangle : IGridPointHost
    {
        public List<GridPoint> GridPoints { get; set; }
        public MeshSolid Host { get; set; }
        public int[] VerticesIndex { get; set; }
        public int[] EdgeIndex { get; set; }

        public TriangleType TriangleType { get; private set; } = TriangleType.Common;

        public MeshTriangle(MeshSolid host, int[] verticesIndex)
        {
            this.Host = host;
            this.VerticesIndex = verticesIndex;
            this.GridPoints = new List<GridPoint>();
            EdgeIndex = new int[3];
        }

        public Vec3 Get_Vertex(int index)
        {
            return this.Host.Vertices[this.VerticesIndex[index]];
        }
        public double Get_Area()
        {
            var v0=this.Get_Vertex(0);
            var v1 = this.Get_Vertex(1);
            var v2=this.Get_Vertex (2);
            var v01 = v1 - v0;
            var v02 = v2 - v0;
            return v01.CrossProduct(v02).GetLength();
        }
        public double Get_ProjectionArea()
        {
            var v0 = this.Get_Vertex(0);
            var v1 = this.Get_Vertex(1);
            var v2 = this.Get_Vertex(2);
            var v01 = v1 - v0;
            var v02 = v2 - v0;
            var v01New = new Vec3(v01.X, v01.Y, 0);
            var v02New=new Vec3(v02.X ,v02.Y,0);
            return v01New.CrossProduct(v02New).GetLength();

        }
       
        
        
        public void GenerateGridPoints(Vec3 origin, double voxelSize)
        {
            //Get triangle type
            this.TriangleType = GetTriangleType(origin, voxelSize);
            this.GridPoints = new List<GridPoint>();
            //GetTriangleNormal n
            int[] triVertices = this.VerticesIndex;
            int vi0 = triVertices[0];
            int vi1 = triVertices[1];
            int vi2 = triVertices[2];

            Vec3 v0 = this.Host.Vertices[vi0];
            Vec3 v1 = this.Host.Vertices[vi1];
            Vec3 v2 = this.Host.Vertices[vi2];
            //get triangle boundary
            int colMin = int.MaxValue;
            int colMax = int.MinValue;
            int rowMin = int.MaxValue;
            int rowMax = int.MinValue;
            //compute triNorm
            var vec01 = v1 - v0;
            var vec12 = v2 - v1;
            var norm = (vec01.CrossProduct(vec12)).Normalize();
            for (int i = 0; i <= 2; i++)
            {
                var p0 = this.Get_Vertex(i);
                var dist = p0 - origin;
                var colMinTemp = (int)Math.Ceiling(dist.X / voxelSize) - 1;
                var colMaxTemp = (int)Math.Floor(dist.X / voxelSize) + 1;
                var rowMinTemp = (int)Math.Ceiling(dist.Y / voxelSize) - 1;
                var rowMaxTemp = (int)Math.Floor(dist.Y / voxelSize) + 1;
                colMin = Math.Min(colMinTemp, colMin);
                colMax = Math.Max(colMaxTemp, colMax);
                rowMin = Math.Min(rowMinTemp, rowMin);
                rowMax = Math.Max(rowMaxTemp, rowMax);
            }
            //determine if the innerGps(if any) is generated by col or rows
            int colRng = colMax - colMin + 1;
            int rowRng = rowMax - rowMin + 1;
            GridPoint[] colBdryElev_Max = new GridPoint[colRng];
            GridPoint[] colBdryElev_Min = new GridPoint[colRng];
            for (int i = 0; i <= 2; i++)
            {
                //scan vertices
                var p0 = this.Get_Vertex(i % 3);
                var p1 = this.Get_Vertex((i + 1) % 3);
                var gpP0 = new GridPoint(p0, origin, voxelSize);
                this.GridPoints.Add(gpP0);
                if (gpP0.GridType == GridPointType.CGP || gpP0.GridType ==GridPointType.IGP) 
                {
                    UpdateGridPointRowRange(gpP0,colMin,ref colBdryElev_Min, ref colBdryElev_Max);
                }
                //scan col
                List<GridPoint> gpScanCol = GetGridPointAlongXAxis(p0, p1, origin, voxelSize, true);
                this.GridPoints.AddRange(gpScanCol);
                foreach (var gp in gpScanCol)
                {
                    UpdateGridPointRowRange(gp, colMin, ref colBdryElev_Min, ref colBdryElev_Max);
                }
                bool addIGPWhenScanRow = false;
                if(this.TriangleType ==TriangleType.ColOverlap)
                {
                    addIGPWhenScanRow = true;
                }
                //scan row
                List<GridPoint> gpScanRow = GetGridPointAlongYAxis(p0, p1, origin, voxelSize, addIGPWhenScanRow);
                this.GridPoints.AddRange(gpScanRow);
            }
            if (Math.Round(norm.Z, 4) != 0) ////generate inner gp, only for non-vertical voxels
            {
                for (int col = 0; col <= colRng - 1; col++)
                {
                    var p0 = colBdryElev_Min[col];
                    var p1 = colBdryElev_Max[col];
                    if (p0 != null && p1 != null && p0.Row != p1.Row)
                    {
                        var col2Scan = p0.Column;
                        var rowSt = p0.Row + 1;
                        var rowEd = p1.Row;
                        if (p1.GridType == GridPointType.IGP)// rowed minus 1
                        {
                            rowEd -= 1;
                        }
                        List<GridPoint> igps = GetInnerPtByCol(col2Scan, rowSt, rowEd, v0, norm, origin, voxelSize);
                        if (igps != null)
                        {
                            this.GridPoints.AddRange(igps);
                        }
                    }
                }
            }
        }
      
        public void UpdateGridPointRowRange(GridPoint pt, int colMin,  ref GridPoint[] min,ref GridPoint[] max)
        {
            var colLoc = pt.Column - colMin;
            //update max
            if (max[colLoc] == null)
            {
                max[colLoc] = pt;
            }
            else
            {
                var gpExist = max[colLoc];
                if (gpExist.Row < pt.Row)
                {
                    max[colLoc] = pt;
                }
            }
            //update min
            if (min[colLoc] == null)
            {
                min[colLoc] = pt;
            }
            else
            {
                var gpExist = min[colLoc];
                if (gpExist.Row > pt.Row)
                {
                    min[colLoc] = pt;
                }
            }
        }
        private List<GridPoint> GetGridPointAlongXAxis(Vec3 pt0,Vec3 pt1,Vec3 origin,double voxSize,bool includIGP)
        {
            //Get col range excluding the edge vertices
            List<GridPoint> result = new List<GridPoint>();
            double xSt = Math.Min(pt0.X, pt1.X);
            double xEd = Math.Max(pt0.X, pt1.X);
            double dblCol0 = Math.Round((xSt - origin.X) / voxSize, 4);
            double dblCol1 = Math.Round((xEd- origin.X) / voxSize, 4);
            int colSt = (int)Math.Floor(dblCol0)+1;
            int colEd = (int)Math.Ceiling(dblCol1)-1;
            if (colSt>colEd) //if so, ignore the col range
            {
                return result;
            }
            else
            {
                for(int col = colSt; col <=colEd; col++)
                {
                    double x = origin.X + col * voxSize;
                    var colPlaneOrigin = new Vec3(x, 0, 0);
                    Vec3 edgeDir = (pt1- pt0);
                    double paramY_X = edgeDir.Y / edgeDir.X;
                    double paramZ_X = edgeDir.Z / edgeDir.X;
                    double y = paramY_X * (x - pt0.X) + pt0.Y;
                    double z = paramZ_X * (x - pt0.X) + pt0.Z;
                    GridPoint gp = new GridPoint(new Vec3(x, y, z),origin,voxSize);
                    if(gp.GridType !=GridPointType.IGP || includIGP)
                    {
                        result.Add(gp);
                    }
                }
            }
            return result;
        }

        private List<GridPoint> GetGridPointAlongYAxis(Vec3 pt0, Vec3 pt1, Vec3 origin, double voxSize, bool includIGP)
        {
            //Get col range excluding the edge vertices
            List<GridPoint> result = new List<GridPoint>();
            double ySt = Math.Min(pt0.Y, pt1.Y);
            double yEd = Math.Max(pt0.Y, pt1.Y);
            double dblRow0 = Math.Round((ySt - origin.Y) / voxSize, 4);
            double dblRow1 = Math.Round((yEd - origin.Y) / voxSize, 4);
            int rowSt = (int)Math.Floor(dblRow0) + 1;
            int rowEd = (int)Math.Ceiling(dblRow1) - 1;
            if (rowSt > rowEd) //if so, ignore the rowl range
            {
                return result;
            }
            else
            {
                for (int row = rowSt; row <= rowEd; row++)
                {
                    double y = origin.Y + row * voxSize;
                    var rowPlaneOrigin = new Vec3(0, y, 0);
                    Vec3 edgeDir = (pt1 - pt0);
                    double paramX_Y = edgeDir.X / edgeDir.Y;
                    double paramZ_Y = edgeDir.Z / edgeDir.Y;
                    double x = paramX_Y * (y - pt0.Y) + pt0.X;
                    double z = paramZ_Y * (y - pt0.Y) + pt0.Z;
                    GridPoint gp = new GridPoint(new Vec3(x, y, z), origin, voxSize);
                    if (gp.GridType != GridPointType.IGP || includIGP)
                    {
                        result.Add(gp);
                    }
                }
            }
            return result;
        }

       
        private List<int> GetColRange(Vec3 pt0, Vec3 pt1, Vec3 origin, double voxSize)
        {
            List<int> result = new List<int>();
            double dblCol0 = Math.Round((pt0.X - origin.X) / voxSize, 4);
            double dblCol1 = Math.Round((pt1.X - origin.X) / voxSize, 4);
            int col0 = (int)Math.Floor(dblCol0);
            int col1= (int)Math.Floor(dblCol1);
            var colSt = col0;
            var colEd = col1;
            if(col0==col1)
            {
                if(dblCol0 ==col0) //the edge falls in a col plane
                {
                    result.Add(col0);
                    return result;
                }
                else //the edge falls between 2 cols, no scanning needed
                {
                    return result;
                }
            }
            else if(col0<col1)
            {
                //calculate colSt
                if(col0==dblCol0) //st falls on a col plane
                {
                    colSt = col0;
                }
                else //the plane indexes col0 falls at the left of the edge range, offset colSt to the right
                {
                    colSt = col0 + 1;
                }
                //calculate colEd
                if(col1==dblCol1) //ed falls on a col plane, we do not need to check ptEd, so the col move left with 1
                {
                    colEd = col1-1;
                }
                else //ed is within the scan range,
                {
                    colEd = col1;
                }
                //validation check: if colEd is no smaller than colSt then output the result
                for (int col = colSt; col <= colEd; col++)
                {
                    result.Add(col);
                }
                return result;
            }
            else //col0>col1 
            {
                //calculate colSt
                colSt =col0;
                colEd = col1 + 1;
                //validation check: if colEd is no bigger than colSt then output the result
                for (int col = colSt; col >= colEd; col--)
                {
                    result.Add(col);
                }
                return result;
            }
        }
        private List<int> GetRowRange(Vec3 pt0, Vec3 pt1, Vec3 origin, double voxSize)
        {
            List<int> result = new List<int>();
            double dblRow0 = Math.Round((pt0.Y - origin.Y) / voxSize, 4);
            double dblRow1 = Math.Round((pt1.Y - origin.Y) / voxSize, 4);
            int row0 = (int)Math.Floor(dblRow0);
            int row1 = (int)Math.Floor(dblRow1);
            var  rowSt = row0;
            var rowEd = row1;
            if (row0 == row1) 
            {
                if(dblRow0 == row0 )//the pt0 falls in a row plane
                {
                    result.Add(row0);
                    return result;
                }
                else//the edge falls between 2 row planes
                {
                    return result;
                }
            }
            else if(row0<row1)
            { 
                //check st
                if(dblRow0 ==row0)
                {
                    rowSt = row0;
                }
                else
                {
                    rowSt = row0 + 1;
                }
                //check ed
                if(dblRow1 ==row1)
                {
                    rowEd = row1 - 1;
                }
                else
                {
                    rowEd = row1;
                }
                for(int row =rowSt; row <= rowEd; row++)
                {
                    result.Add(row);
                }
                return result;
            }
            else //row0>row1
            {
                rowSt = row0;
                rowEd = row1 + 1;
                for(int row =rowSt; row >= rowEd; row--)
                {
                    result.Add (row);
                }
                return result;
            }

        }
       

        private Vec3 ColPlaneIntersectEdge(int col,Vec3 cellOrigin,double voxelSize,Vec3 triOrigin, Vec3 triNorm, Vec3 edgeStInclusive, Vec3 edgeEdExclusive)
        {
            double x = cellOrigin.X + col * voxelSize;
            var colPlaneOrigin = new Vec3(x, 0, 0);
            Vec3 edgeDir= (edgeEdExclusive - edgeStInclusive);
            if (Math.Round(edgeDir.X, 4) != 0)
            {
                double paramY_X = edgeDir.Y / edgeDir.X;
                double paramZ_X = edgeDir.Z / edgeDir.X;
                double y = paramY_X * (x - edgeStInclusive.X) + edgeStInclusive.Y;
                double z = paramZ_X * (x - edgeStInclusive.X) + edgeStInclusive.Z;
                return new Vec3(x, y, z);
            }
            else //line parallel to tri
            {
                if (Math.Round(edgeStInclusive.X - x, 4) == 0) // the pt is in the plane
                {
                    return edgeStInclusive;
                }
                return null;
            }
        }
        private Vec3 RowPlaneIntersectEdge(int row, Vec3 cellOrigin, double voxelSize, Vec3 triOrigin, Vec3 triNorm, Vec3 edgeStInclusive, Vec3 edgeEdExclusive)
        {
            double y = cellOrigin.Y + row * voxelSize;
            Vec3 edgeDir = (edgeEdExclusive - edgeStInclusive);
            //check if the point is within a valid edge
            if (Math.Round(edgeDir.Y, 4) != 0) //the line is not parallel to the plane
            {
                double paramX_Y = edgeDir.X / edgeDir.Y;
                double paramZ_Y = edgeDir.Z / edgeDir.Y;
                double x = paramX_Y * (y - edgeStInclusive.Y) + edgeStInclusive.X;
                double z = paramZ_Y * (y - edgeStInclusive.Y) + edgeStInclusive.Z;
                return new Vec3(x, y, z);
            }
            else //line parallel to tri
            {
                //in-plane check
                if (Math.Round((y - edgeStInclusive.Y), 4) == 0) // the pt is in the plane
                {
                    return edgeStInclusive;
                }
                return null;
            }
        }
        private List<GridPoint> GetInnerPtByCol(int col, int rowSt,int rowEd,Vec3 triOrigin, Vec3 triNorm,Vec3 origin, double voxelSize)
        {
            if(Math.Round (triNorm.Z ,4)==0) //vertical triangle
            {
                return null;
            }
            List<GridPoint> result = new List<GridPoint>();
            var v0 = triOrigin;
            double paramXZ = triNorm.X / triNorm.Z;
            double paramYZ = triNorm.Y / triNorm.Z;
            for (int row = rowSt; row <= rowEd; row++)
            {
                double x = col * voxelSize + origin.X;
                double y = row * voxelSize + origin.Y;
                double z = v0.Z - paramXZ * (x - v0.X) - paramYZ * (y - v0.Y);
                GridPoint IGP = new GridPoint(x, y, col, row, z, GridPointType.IGP);
                result.Add(IGP);
            }
            return result;
        }
        
        /// <summary>
        /// Determine the triangle type
        /// the type is :normal,col,row
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="voxelSize"></param>
        public TriangleType GetTriangleType(Vec3 origin,double voxelSize)
        {
            //return TriangleType.Common;
            //check if the triangle overlaps to the row or col plane
            //if a triangle parallel to  and only contains CGP/RGP and IGP, 
            var pt0 = this.Host.Vertices[this.VerticesIndex[0]];
            var pt1 = this.Host.Vertices[this.VerticesIndex[1]];
            var pt2 = this.Host.Vertices[this.VerticesIndex[2]];
            var vec01 = pt1 - pt0;
            var vec02 = pt2 - pt0;
            
            if (Math.Round( vec01.DotProduct(Vec3.BasisX),4) == 0 && Math.Round ( vec02.DotProduct(Vec3.BasisX),4) == 0) //the triangle overlaps with the col plane
            {
                var dblCol = Math.Round((pt0.X - origin.X) / voxelSize, 4);
                var colMin = Math.Ceiling(dblCol);
                var colMax = Math.Floor(dblCol);
                if(colMin ==colMax)
                {
                    return TriangleType.ColOverlap;
                }
                else
                {
                    return TriangleType.Common;
                }
            }
            else if (Math.Round (  vec01.DotProduct(Vec3.BasisY),4) == 0 && Math.Round ( vec02.DotProduct(Vec3.BasisY),4) == 0) //the triangle overlaps with the row plane
            {
                var dblRow = Math.Round((pt0.Y - origin.Y) / voxelSize, 4);
                var rowMin = Math.Ceiling(dblRow);
                var rowMax = Math.Floor(dblRow);
                if(rowMax ==rowMin )
                {
                    return TriangleType.RowOverlap;
                }
                else
                {
                    return TriangleType.Common;
                }
            }
            else
            {
                return TriangleType.Common;
            }
        }
        public IEnumerable<Voxel> GenerateVoxels()
        {

            //Get All GPs in Triangle to find the col and row range
            int rowMax = int.MinValue;
            int rowMin = int.MaxValue;
            int colMax = int.MinValue;
            int colMin = int.MaxValue;
            
            //List<GridPoint> gps = this.GetAllGridPoints().ToList();
            for (int i=0;i<=2;i++)
            {
                GridPoint vgp = this.Host.GridPoints[this.VerticesIndex[i]];
                int rowMaxTemp = vgp.Row;
                int rowMinTemp = vgp.Row;
                if (vgp.GridType == GridPointType.RGP || vgp.GridType == GridPointType.IGP)
                    rowMinTemp -= 1;
                int colMinTemp = vgp.Column;
                if (vgp.GridType == GridPointType.IGP || vgp.GridType == GridPointType.CGP)
                    colMinTemp -= 1;
                int colMaxTemp = vgp.Column;
                rowMax = Math.Max(rowMax, rowMaxTemp);
                rowMin = Math.Min(rowMin, rowMinTemp);
                colMax = Math.Max(colMax, colMaxTemp);
                colMin = Math.Min(colMin, colMinTemp);
            }
            //modify row and col max
            int colRng = colMax - colMin + 1;
            int rowRng = rowMax - rowMin + 1;
            //Create an array arrarrGPAffRng(colRng,rowRng) to group each gps
            //VGP: can only affect the range[col,row]
            //CGP: can only affect the range[col-1,row],[col,row];
            //RGP:can only affect the range[col,row-1],[col,row];
            //IGP: can affect the range[col-1,row-1], [col,row-1],[col-1,row],[col,row]
            List<GridPoint>[,] arrGPAffRng = new List<GridPoint>[colRng, rowRng];
            for (int i = 0; i < colRng; i++)
            {
                for (int j = 0; j < rowRng; j++)
                {
                    arrGPAffRng[i, j] = new List<GridPoint>() { Capacity =4};
                }
            }
            
            foreach (var gp in this.GridPoints)
            {
                int colLoc = gp.Column - colMin;
                int rowLoc = gp.Row - rowMin;
                switch (gp.GridType)
                {
                    case GridPointType.VGP:
                        arrGPAffRng[colLoc, rowLoc].Add(gp);
                        break;
                    case GridPointType.CGP:
                        arrGPAffRng[colLoc, rowLoc].Add(gp);
                        if (colLoc - 1 >= 0)
                            arrGPAffRng[colLoc - 1, rowLoc].Add(gp);
                        break;
                    case GridPointType.RGP:
                        arrGPAffRng[colLoc, rowLoc].Add(gp);
                        if(rowLoc -1>=0)
                            arrGPAffRng[colLoc, rowLoc - 1].Add(gp);
                        break;
                    case GridPointType.IGP:
                        arrGPAffRng[colLoc, rowLoc].Add(gp);
                        if(rowLoc >0 && colLoc >0)
                        {
                            arrGPAffRng[colLoc, rowLoc - 1].Add(gp);
                            arrGPAffRng[colLoc - 1, rowLoc].Add(gp);
                            arrGPAffRng[colLoc - 1, rowLoc - 1].Add(gp);
                        }
                        else if(rowLoc>0)
                        {
                            arrGPAffRng[colLoc, rowLoc - 1].Add(gp);
                        }
                        else if(colLoc >0)
                        {
                            arrGPAffRng[colLoc - 1, rowLoc].Add(gp);
                        }
                        break;
                }
            }
            //get triangle type
            TriangleType triType = this.TriangleType;
            //Generate voxels
            for (int colLoc = 0; colLoc < colRng; colLoc++)
            {
                for (int rowLoc = 0; rowLoc < rowRng; rowLoc++)
                {
                    var item = arrGPAffRng[colLoc, rowLoc];
                    if (item.Count >= 3)
                    {
                        //create an voxel 
                        double voxUpperElev = item.Max(c => c.Z);
                        double voxLowerElev = item.Min(c => c.Z);
                        Voxel vox= null;
                        switch (triType)
                        {
                            case TriangleType.Common:
                                vox = new Voxel(this.Host.Owner, colLoc + colMin, rowLoc + rowMin, voxLowerElev, voxUpperElev);
                                break;
                            case TriangleType.ColOverlap:
                                vox = new Voxel(this.Host.Owner, colLoc + colMin, rowLoc + rowMin, voxLowerElev, voxUpperElev);
                                vox.VoxType = VoxelType.Odd;
                                vox.ForbiddenDirections_Top = new bool[4];
                                vox.ForbiddenDirections_Bottom=new bool[4];
                                if(colLoc ==0) //the voxel is at the left of the col plane, mark its east as forbbidden
                                {
                                   
                                    vox.ForbiddenDirections_Top[0]=true;
                                    vox.ForbiddenDirections_Bottom[0] = true;
                                }
                                else //at the right
                                {
                                    vox.ForbiddenDirections_Top[2] = true;
                                    vox.ForbiddenDirections_Bottom[2] = true;
                                }
                                break;
                            case TriangleType.RowOverlap:
                                vox = new Voxel(this.Host.Owner, colLoc + colMin, rowLoc + rowMin, voxLowerElev, voxUpperElev);
                                vox.VoxType = VoxelType.Odd;
                                vox.ForbiddenDirections_Bottom = new bool[4];
                                vox.ForbiddenDirections_Top = new bool[4];
                                if (rowLoc ==0) //the voxel is at the south of the row plane mark its north(1) as forbidden 
                                {
                                    vox.ForbiddenDirections_Top[1] = true;
                                    vox.ForbiddenDirections_Bottom[1] = true;
                                }
                                else //north
                                {
                                    vox.ForbiddenDirections_Top[3] = true;
                                    vox.ForbiddenDirections_Bottom[3] = true;
                                }
                                break; 
                        }
                        yield return vox;
                    }
                }
            }
        }
        private List<GridPoint>[,] arrGPAffRng;//the affectiong range of each GP
        private int colMin;//col min index
        private int rowMin;// row min index
        public IEnumerable<Voxel> GenerateVoxels_Parallel(Vec3 origin,double voxSize)
        {
            var colRng = arrGPAffRng.GetUpperBound(0);
            var rowRng = arrGPAffRng.GetUpperBound(1);
           
            foreach (var gp in this.GridPoints)
            {
                int colLoc = gp.Column - colMin;
                int rowLoc = gp.Row - rowMin;
                switch (gp.GridType)
                {
                    case GridPointType.VGP:
                        arrGPAffRng[colLoc, rowLoc].Add(gp);
                        break;
                    case GridPointType.CGP:
                        arrGPAffRng[colLoc, rowLoc].Add(gp);
                        if (colLoc - 1 >= 0)
                            arrGPAffRng[colLoc - 1, rowLoc].Add(gp);
                        break;
                    case GridPointType.RGP:
                        arrGPAffRng[colLoc, rowLoc].Add(gp);
                        if (rowLoc - 1 >= 0)
                            arrGPAffRng[colLoc, rowLoc - 1].Add(gp);
                        break;
                    case GridPointType.IGP:
                        arrGPAffRng[colLoc, rowLoc].Add(gp);
                        if (rowLoc > 0 && colLoc > 0)
                        {
                            arrGPAffRng[colLoc, rowLoc - 1].Add(gp);
                            arrGPAffRng[colLoc - 1, rowLoc].Add(gp);
                            arrGPAffRng[colLoc - 1, rowLoc - 1].Add(gp);
                        }
                        else if (rowLoc > 0)
                        {
                            arrGPAffRng[colLoc, rowLoc - 1].Add(gp);
                        }
                        else if (colLoc > 0)
                        {
                            arrGPAffRng[colLoc - 1, rowLoc].Add(gp);
                        }
                        break;
                }
            }
            //get triangle type
            TriangleType triType = this.TriangleType;
            //Generate voxels
            for (int colLoc = 0; colLoc < colRng; colLoc++)
            {
                for (int rowLoc = 0; rowLoc < rowRng; rowLoc++)
                {
                    var item = arrGPAffRng[colLoc, rowLoc];
                    if (item.Count >= 3)
                    {
                        //create an voxel 
                        double voxUpperElev = item.Max(c => c.Z);
                        double voxLowerElev = item.Min(c => c.Z);
                        Voxel vox = null;
                        switch (triType)
                        {
                            case TriangleType.Common:
                                vox = new Voxel(this.Host.Owner, colLoc + colMin, rowLoc + rowMin, voxLowerElev, voxUpperElev);
                                break;
                            case TriangleType.ColOverlap:
                                vox = new Voxel(this.Host.Owner, colLoc + colMin, rowLoc + rowMin, voxLowerElev, voxUpperElev);
                                vox.VoxType = VoxelType.Odd;
                                vox.ForbiddenDirections_Top = new bool[4];
                                vox.ForbiddenDirections_Bottom = new bool[4];
                                if (colLoc == 0) //the voxel is at the left of the col plane, mark its east as forbbidden
                                {

                                    vox.ForbiddenDirections_Top[0] = true;
                                    vox.ForbiddenDirections_Bottom[0] = true;
                                }
                                else //at the right
                                {
                                    vox.ForbiddenDirections_Top[2] = true;
                                    vox.ForbiddenDirections_Bottom[2] = true;
                                }
                                break;
                            case TriangleType.RowOverlap:
                                vox = new Voxel(this.Host.Owner, colLoc + colMin, rowLoc + rowMin, voxLowerElev, voxUpperElev);
                                vox.VoxType = VoxelType.Odd;
                                vox.ForbiddenDirections_Bottom = new bool[4];
                                vox.ForbiddenDirections_Top = new bool[4];
                                if (rowLoc == 0) //the voxel is at the south of the row plane mark its north(1) as forbidden 
                                {
                                    vox.ForbiddenDirections_Top[1] = true;
                                    vox.ForbiddenDirections_Bottom[1] = true;
                                }
                                else //north
                                {
                                    vox.ForbiddenDirections_Top[3] = true;
                                    vox.ForbiddenDirections_Bottom[3] = true;
                                }
                                break;
                        }
                        yield return vox;
                    }
                }
            }
        }
        /// <summary>
        /// Allocate potential memeory requirement for triangle voxelization
        /// </summary>
        /// <param name="vox"></param>
        /// <returns></returns>
        public void GenerateGridPoints_Parallel(Vec3 origin,double voxSize)
        {
            GenerateGridPoints(origin, voxSize);
            //Get All GPs in Triangle to find the col and row range
            int rowMax = int.MinValue;
            int rowMin = int.MaxValue;
            int colMax = int.MinValue;
            int colMin = int.MaxValue;
            //List<GridPoint> gps = this.GetAllGridPoints().ToList();
            for (int i = 0; i <= 2; i++)
            {
                GridPoint vgp = this.Host.GridPoints[this.VerticesIndex[i]];
                int rowMaxTemp = vgp.Row;
                int rowMinTemp = vgp.Row;
                if (vgp.GridType == GridPointType.RGP || vgp.GridType == GridPointType.IGP)
                    rowMinTemp -= 1;
                int colMinTemp = vgp.Column;
                if (vgp.GridType == GridPointType.IGP || vgp.GridType == GridPointType.CGP)
                    colMinTemp -= 1;
                int colMaxTemp = vgp.Column;
                rowMax = Math.Max(rowMax, rowMaxTemp);
                rowMin = Math.Min(rowMin, rowMinTemp);
                colMax = Math.Max(colMax, colMaxTemp);
                colMin = Math.Min(colMin, colMinTemp);
            }
            //modify row and col max
            int colRng = colMax - colMin + 1;
            int rowRng = rowMax - rowMin + 1;
            //Create an array arrarrGPAffRng(colRng,rowRng) to group each gps
            //VGP: can only affect the range[col,row]
            //CGP: can only affect the range[col-1,row],[col,row];
            //RGP:can only affect the range[col,row-1],[col,row];
            //IGP: can affect the range[col-1,row-1], [col,row-1],[col-1,row],[col,row]
            List<GridPoint>[,] arrGPAffRng = new List<GridPoint>[colRng, rowRng];
            for (int i = 0; i < colRng; i++)
            {
                for (int j = 0; j < rowRng; j++)
                {
                    arrGPAffRng[i, j] = new List<GridPoint>() { Capacity = 4 };
                }
            }
            
            this.arrGPAffRng = arrGPAffRng;
            this.colMin = colMin;
            this.rowMin = rowMin;
        }
        
        public IEnumerable<GridPoint> GetAllGridPoints()
        {
            foreach (var pt in this.GridPoints)
            {
                yield return pt;
            }
        }
    }
    public class GPColComparer : IComparer<GridPoint>
    {
        public int Compare(GridPoint x, GridPoint y)
        {
            var colX = x.Column;
            var colY = y.Column;
            if(colX < colY)
            {
                return -1;
            }
            else if(colX ==colY)
            {
                if(x.GridType==GridPointType.CGP || x.GridType==GridPointType.IGP)
                {
                    return -1;
                }
                else if((y.GridType == GridPointType.CGP || y.GridType == GridPointType.IGP))
                {
                    return 1;
                }
                else
                {
                    return 0;
                }
            }
            else
            {
                return 1;
            }
            
           //throw new NotImplementedException();
        }
    }

    public class Vec3:IEquatable<Vec3>
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public Vec3()
        {
        }
        public Vec3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static Vec3 operator +(Vec3 v1,Vec3 v2)
        {
            return new Vec3(v1.X + v2.X, v1.Y + v2.Y, v1.Z + v2.Z);
        }
        public static Vec3 operator -(Vec3 v1, Vec3 v2)
        {
            return new Vec3(v1.X - v2.X, v1.Y - v2.Y, v1.Z - v2.Z);
        }
        public static Vec3 operator *(double num,Vec3 v)
        {
            return new Vec3(num * v.X, num * v.Y, num * v.Z);
        }
        public static Vec3 operator *(Vec3 v, double num)
        {
            return new Vec3(num * v.X, num * v.Y, num * v.Z);
        }
        public static Vec3 operator /(Vec3 v, double num)
        {
            return v*(1/num);
        }

        public double DotProduct(Vec3 other)
        {
            return this.X * other.X + this.Y * other.Y + this.Z * other.Z;
        }

        public Vec3 CrossProduct(Vec3 other)
        {
            double x= this.Y * other.Z - this.Z * other.Y;
            double y = -this.X * other.Z +this.Z * other.X;
            double z= this.X * other.Y - this.Y * other.X;
            return new Vec3(x, y, z);
        }

        public double GetSquareLen()
        {
            return this.DotProduct(this);
        }
        public double GetLength()
        {
            return Math.Sqrt(GetSquareLen());
        }
        public Vec3 Normalize()
        {
            double dblLen = this.GetLength();
            return new Vec3(this.X / dblLen, this.Y / dblLen, this.Z / dblLen);
        }
        public override int GetHashCode()
        {
            return this.ToString().GetHashCode();
            //return base.GetHashCode();
        }
        public static Vec3 BasisX
        {
            get
            {
               return new Vec3(1, 0, 0);
            }  
        }
        public static Vec3 BasisY
        {
            get
            {
                return new Vec3(0, 1, 0);
            }
        }
        public static Vec3 BasisZ
        {
            get
            {
                return new Vec3(0, 0, 1);
            }
        }
        public static Vec3 Zero
        {
            get
            {
                return new Vec3(0, 0, 0);
            }
        }
        public override string ToString()
        {
            string strX = this.X.ToString("0.0000");
            string strY = this.Y.ToString("0.0000");
            string strZ = this.Z.ToString("0.0000");
            return string.Format("{0},{1},{2}", strX, strY, strZ);
        }
        public string ToString(string saparater)
        {
            string strX = this.X.ToString("0.0000");
            string strY = this.Y.ToString("0.0000");
            string strZ = this.Z.ToString("0.0000");
            return string.Join(saparater, strX, strY, strZ);
        }
        
        public bool Equals(Vec3 other)
        {
            return (this.ToString() == other.ToString());
        }
    }

    public class Vec2
    {
        public double U { get; set; }
        public double V { get; set; }
        public Vec2(double u,double v)
        {
            this.U = u;
            this.V = v;
        }
        public static Vec2 operator +(Vec2 a,Vec2 b)
        {
            return new Vec2(a.U + b.U, a.V + b.V);
        }
        public static Vec2 operator -(Vec2 a, Vec2 b)
        {
            return new Vec2(a.U - b.U, a.V - b.V);
        }
        public static Vec2 operator *(double num, Vec2 a)
        {
            return new Vec2(num*a.U ,num*a.V);
        }
        public static Vec2 operator *( Vec2 a, double num)
        {
            return new Vec2(num * a.U, num * a.V);
        }
        public double DotProcuct(Vec2 other)
        {
            return this.U * other.U + this.V * other.V;
        }
    }
    public class Matrix2x2
    {
        public Vec2 Column0 { get; set; }
        public Vec2 Column1 { get; set; }
        public Matrix2x2(Vec2 colVec1,Vec2 colVec2)
        {
            this.Column0 = colVec1;
            this.Column1 = colVec2;
        }
        public double GetDet()
        {
            double u0 = Column0.U;
            double v0 = Column0.V;
            double u1 = Column1.U;
            double v1 = Column1.V;
            return (u0 * v1 - v0 * u1);
        }
        public Matrix2x2 ReplaceColumn(int columnIndex,Vec2 newColumn)
        {
            if(columnIndex ==0)
            {
                return new Matrix2x2(newColumn, this.Column1);

            }
            else if(columnIndex ==1)
            {
                return new Matrix2x2(this.Column0, newColumn);
            }
            else
            {
                throw new InvalidOperationException("the index should be either 0 or 1");
            }
        }
        public static Vec2 SolveFolumaXY(Matrix2x2 xishuMatrix,Vec2 resultMatrix)
        {
            var fenmu = xishuMatrix.GetDet();
            var fenziMu = xishuMatrix.ReplaceColumn(0, resultMatrix).GetDet();
            var fenziDelta = xishuMatrix.ReplaceColumn(1, resultMatrix * (-1)).GetDet();
            var mu = fenziMu / fenmu;
            var delta = fenziDelta / fenmu;
            return new Vec2(mu, delta);
        }
    }

    public class Transform
    {
        public Vec3 BasisX { get; set; }
        public Vec3 BasisY { get; set; }
        public Vec3 BasisZ { get; set; }
        public Vec3 Origin { get; set; }

        public Transform(Vec3 bassiX,Vec3 basisY,Vec3 basisZ,Vec3 origin)
        {
            BasisX = bassiX;
            BasisY = basisY;
            BasisZ = basisZ;
            Origin = origin;
        }
        public static Transform Idnentity { get; } = new Transform(Vec3.BasisX, Vec3.BasisY, Vec3.BasisZ, Vec3.Zero);
        public Vec3 OfPoint(Vec3 point)
        {
            var x = point.X * BasisX.X + point.Y * BasisY.X + point.Z * BasisZ.X + Origin.X;
            var y = point.X * BasisX.Y + point.Y * BasisY.Y + point.Z * BasisZ.Y + Origin.Y;
            var z = point.X * BasisX.Z + point.Y * BasisY.Z + point.Z * BasisZ.Z + Origin.Z;
            return new Vec3(x, y, z);
        }
        public Vec3 OfVector(Vec3 vector)
        {
            var x = vector.X * BasisX.X + vector.Y * BasisY.X + vector.Z * BasisZ.X ;
            var y = vector.X * BasisX.Y + vector.Y * BasisY.Y + vector.Z * BasisZ.Y ;
            var z = vector.X * BasisX.Z + vector.Y * BasisY.Z + vector.Z * BasisZ.Z ;
            return new Vec3(x, y, z);
        }
        public Transform Multiply(Transform right)
        {
            var newBasisX = this.OfVector(right.BasisX);
            var newBasisY = this.OfVector(right.BasisY);
            var newBasisZ = this.OfVector(right.BasisZ);
            var newOrigin = this.OfPoint(right.Origin);
            Transform newTransform = new Transform(newBasisX, newBasisY, newBasisZ, newOrigin);
            return new Transform(newBasisX, newBasisY, newBasisZ, newOrigin);
        }

        public List<Vec3> OfPoints(List<Vec3> pts2Transform)
        {
            List<Vec3> result = new List<Vec3>();
            pts2Transform.ForEach(c => result.Add(this.OfPoint(c)));
            return result;
        }
    }
    public class Vec4
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double W { get; set; }
        public Vec4(double x,double y,double z,double w)
        {
            this.X = x;
            this.Y = y;
            this.Z = z;
            this.W = w;
        }
        
        public static double operator *(Vec4 vec1,Vec4 vec2)
        {
            return vec1.X * vec2.X + vec1.Y * vec2.Y + vec1.Z * vec2.Z + vec1.W * vec2.W;
        }

        public static Vec4 ExpandVec3(Vec3 vec, double w)
        {
            return new Vec4(vec.X, vec.Y, vec.Z, w);
        }
    }


    #endregion
    #region Voxel
    public interface IGridPointHost
    {
        List<GridPoint> GridPoints { get; set; }
        void GenerateGridPoints(Vec3 origin, double voxelSize);
        
    }
    public static class VoxelDocumentConverter
    {
        public static void SaveVoxelDocument(VoxelDocument vdoc, string saveFilePath)
        {
            //file format:
            //1. Origin:24bytes;
            List<byte> data = new List<byte>() { Capacity =24};
            data.AddRange(BitConverter.GetBytes(vdoc.Origin.X));
            data.AddRange(BitConverter.GetBytes(vdoc.Origin.Y));
            data.AddRange(BitConverter.GetBytes(vdoc.Origin.Z));
            //2. VoxelSize: 8 bytes
            data.AddRange(BitConverter.GetBytes(vdoc.VoxelSize));
            //3. voxels, per including  Col(4 bytes) Row (4 bytes) bottom Elevation (4 bytes) top elevation (4 bytes),
            Dictionary<Voxel, int> dicVox_Index = new Dictionary<Voxel, int>();
            foreach(var elem in vdoc.Elements)
            {
                foreach (var v in elem.Voxels)
                {
                    dicVox_Index.Add(v,dicVox_Index.Count);
                }
            }
            data.AddRange(BitConverter.GetBytes(dicVox_Index.Keys.Count));
            foreach (var vox_idx in dicVox_Index)
            {
                var vox = vox_idx.Key;
                List<byte> voxBytes = new List<byte>() { Capacity =40};
                voxBytes.AddRange(BitConverter.GetBytes(vox.ColIndex));
                voxBytes.AddRange(BitConverter.GetBytes(vox.RowIndex));
                voxBytes.AddRange (BitConverter.GetBytes(vox.BottomElevation));
                voxBytes.AddRange(BitConverter.GetBytes(vox.TopElevation));
               
                data.AddRange(voxBytes);
            }
            //4. voxELem， per include：ID，Name,Category,VoxelIndexes,isSupport, isActive，isTransport
            List<byte> docData = new List<byte>();
            docData.AddRange(BitConverter.GetBytes(vdoc.Elements.Count));
            foreach (var ve in vdoc.Elements)
            {
                //elemId
                string strId = ve.ElementId;
                var strByte = Encoding.Default.GetBytes(strId);
                var idLen = (byte)strByte.Length;
                docData.Add (idLen);
                docData.AddRange (strByte);
                //name
                string strNa = ve.Name;
                strByte = Encoding.Default.GetBytes(strNa);
                idLen = (byte)strByte.Length;
                docData.Add(idLen);
                docData.AddRange(strByte);
                //category
                string strCat = ve.Category;
                strByte = Encoding.Default.GetBytes(strCat);
                idLen = (byte)strByte.Length;
                docData.Add(idLen);
                docData.AddRange(strByte);
                
                //add voxIndex
                docData.AddRange(BitConverter.GetBytes(ve.Voxels.Count));
                foreach (var v in ve.Voxels)
                {
                    var index = dicVox_Index[v];
                    docData.AddRange (BitConverter.GetBytes(index));
                }
                docData.AddRange(BitConverter.GetBytes(ve.IsSupportElement));
                docData.AddRange(BitConverter.GetBytes(ve.IsActive));
                docData.AddRange(BitConverter.GetBytes(ve.IsTransportElement));
            }
            data.AddRange(docData);
            
            //save file
            FileStream fs = new FileStream(saveFilePath,FileMode.OpenOrCreate);
            fs.Write(data.ToArray(), 0, data.Count);
            fs.Flush();
            fs.Close();
        }
        
        public static void SaveAsCSV(VoxelDocument vdoc,string saveFilePath)
        {
            StreamWriter sw = new StreamWriter(saveFilePath, false, Encoding.Default);
            try
            {
                //export element id, voxel col,voxel row,voxel bottom elevation, voxel top elevation
                string strHeader = "ElementId,ColumnIndex,RowIndex,BottomElevation(mm),TopElevation,voxelSize(mm)";
                sw.WriteLine(strHeader);
                foreach (var ve in vdoc.Elements)
                {
                    string strId = ve.ElementId;
                    string voxelSize = Math.Round(vdoc.VoxelSize * 304.8, 0).ToString();
                    foreach (var v in ve.Voxels)
                    {
                        string strCol = v.ColIndex.ToString();
                        string strRow = v.RowIndex.ToString();
                        string strBtmElev = Math.Round(v.BottomElevation * 304.8, 0).ToString();
                        string strTopElev = Math.Round(v.TopElevation * 304.8, 0).ToString();
                        sw.WriteLine(string.Format("{0},{1},{2},{3},{4},{5}", strId, strCol, strRow, strBtmElev, strTopElev,voxelSize));
                    }
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace);
            }
            finally
            {
                sw.Flush();
                sw.Close();
            }
        }
        public static VoxelDocument LoadVoxelDocuments(ICollection<string> filePaths)
        {
            VoxelDocument voxDocMerge = new VoxelDocument();
            foreach (var fileName in filePaths)
            {
                var doc= LoadVoxelDocument(fileName);
                voxDocMerge.Origin = doc.Origin;
                voxDocMerge.VoxelSize = doc.VoxelSize;
                voxDocMerge.Elements.Capacity = voxDocMerge.Elements.Count + doc.Elements.Count;
                voxDocMerge.Elements.AddRange(doc.Elements);
            }
            return voxDocMerge;
        }
        public static VoxelDocument LoadVoxelDocument(string filePath)
        {
            //1. Origin:24bytes;
            VoxelDocument result = new VoxelDocument();
            FileStream fs = new FileStream(filePath, FileMode.Open);
            BinaryReader br = new BinaryReader(fs, Encoding.Default);
            double[] dblVec3 = new double[3];
            for(int i=0;i<=2;i++)
            {
                dblVec3[i]=  BitConverter.ToDouble(br.ReadBytes(8), 0);
            }
            Vec3 origin=new Vec3(dblVec3[0], dblVec3[1],dblVec3[2]);
            result.Origin = origin;
            //vox size
            double dblVoxSize=BitConverter.ToDouble(br.ReadBytes(8), 0);
            result.VoxelSize = dblVoxSize;
            //Read voxels
            //3.voxels, per including  Col(4 bytes) Row(4 bytes) bottom Elevation(4 bytes) top elevation(4 bytes)
            int numVoxels = BitConverter.ToInt32(br.ReadBytes(4), 0);
            List<Voxel> voxels = new List<Voxel>() { Capacity=numVoxels};
            //List<int[]> voxAdjRel = new List<int[]>();
            for(int i=0;i<=numVoxels-1;i++)
            {
                var vox = new Voxel();
                //read col
                vox.ColIndex=BitConverter.ToInt32(br.ReadBytes(4), 0);
                //read row
                vox.RowIndex= BitConverter.ToInt32(br.ReadBytes(4), 0);
                //read btmElev
                vox.BottomElevation = BitConverter.ToDouble(br.ReadBytes(8), 0);
                //read topElev
                vox.TopElevation= BitConverter.ToDouble(br.ReadBytes(8), 0);
                vox.TopAdjVoxels = new Voxel[4];
                
                voxels.Add(vox);
            }
            //4. voxELem， per include：ID，Category, Name,VoxelIndexes,issppuot, isActive,isTransport
            int numElem = BitConverter.ToInt32(br.ReadBytes(4), 0);
            for(int i=0;i<=numElem-1;i++)
            {
                var voxElem = new VoxelElement();
                voxElem.Voxels = new List<Voxel>();
                result.Elements.Add(voxElem);
                //Id
                byte strLen = br.ReadByte();
                var stringByte=new byte[strLen];
                for(int j=0;j<strLen;j++)
                {
                    stringByte[j] = br.ReadByte();
                }
                var elemId = Encoding.Default.GetString(stringByte);
                voxElem.ElementId = elemId;
                //Name
                strLen = br.ReadByte();
                stringByte = new byte[strLen];
                for (int j = 0; j < strLen; j++)
                {
                    stringByte[j] = br.ReadByte();
                }
                var name = Encoding.Default.GetString(stringByte);
                voxElem.Name = name;
                //Category
                strLen = br.ReadByte();
                stringByte = new byte[strLen];
                for (int j = 0; j < strLen; j++)
                {
                    stringByte[j] = br.ReadByte();
                }
                var cat = Encoding.Default.GetString(stringByte);
                voxElem.Category = cat;
                //vox
                int voxCount = BitConverter.ToInt32(br.ReadBytes(4), 0);
                for(int j=0;j<voxCount;j++)
                {
                    int voxIdx=BitConverter.ToInt32(br.ReadBytes(4), 0);
                    voxElem.Voxels.Add(voxels[voxIdx]);
                }
                voxElem.IsSupportElement = br.ReadBoolean();
                voxElem.IsActive = br.ReadBoolean();
                voxElem.IsTransportElement = br.ReadBoolean();
            }
            return result;
        }

        private static int Read7BitEncodedInt(BinaryReader br)
        {
            int num = 0;
            int num2 = 0;
            byte b;
            do
            {
                b =br. ReadByte();
                num |= (b & 0x7F) << num2;
                num2 += 7;
            }
            while ((b & 0x80u) != 0);
            return num;
        }
    }
    /// <summary>
    /// This class is used for temporarily store voxels
    /// </summary>
    public class TempFileSaver
    {
        private List<byte> buffer;
        private FileStream fs;
        private int bufferSize;
        private string tempPath; //file recording element raw data
       
        public bool DeleteAfterRead { get; set; } = true;
        public int ElementCount { get; set; } = 0;
        //this is used to store the box areas of each element
        public List<double> MeshBoxAreas { get; set; } = new List<double>();
        public VoxelDocument VoxDoc { get; private set; }
        
        public string GetPath()
        {
            return this.tempPath;
        }
        
        public void SetPath(string value,bool deleteAfterRead)
        {
            this.tempPath = value;
            this.DeleteAfterRead = deleteAfterRead;
        }
        public MeshElement MeshElement { get;  set; }
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="doc">Voxel documnet, which assigned the voxel origin and voxel size</param>
        /// <param name="bufferSize">the buffer size in bytes used for temporarily store the voxel element
        /// the data will be transfered to temp files after when the buffer is full, meanwhile the buffer is cleared for
        /// new data to add</param>
        public TempFileSaver(VoxelDocument doc,int bufferSize)
        {
            this.VoxDoc = doc;
            string elementRawFile= Path.GetTempFileName();
            tempPath = elementRawFile;
            this. bufferSize = bufferSize;
            fs=new FileStream (elementRawFile, FileMode.Create);
            buffer = new List<byte>() { Capacity = bufferSize };
            //file format:
            //1. Origin:24bytes;
            buffer.AddRange(BitConverter.GetBytes(doc.Origin.X));
            buffer.AddRange(BitConverter.GetBytes(doc.Origin.Y));
            buffer.AddRange(BitConverter.GetBytes(doc.Origin.Z));
            //2. VoxelSize: 8 bytes
            buffer.AddRange(BitConverter.GetBytes(doc.VoxelSize));
        }
        /// <summary>
        /// try add voxel to buffer or temp files depending if the data in the buffer exceeds the
        /// Given buffer size
        /// </summary>
        /// <param name="voxElem">the voxeled elemets to add</param>
        public void WriteVoxelElement(VoxelElement voxElem)
        {
            var elemData = ConvertElement2Bytes(voxElem, out var dataSize);
            buffer.AddRange(elemData);
            BinaryWriter bw = new BinaryWriter(fs);
            if(buffer.Count >bufferSize) //write file
            {
                fs.Position = fs.Length;
                fs.Write(buffer.ToArray(), 0, buffer.Count);
                fs.Flush();
                //clear buffer
                buffer.Clear();
            }
        }
        /// <summary>
        /// Write the remaining data(if any) in the buffer to the temp file
        /// And terminate the writting process
        /// </summary>
        public void Finish()
        {
            if(buffer.Count !=0)
            {
                fs.Write(buffer.ToArray(), 0, buffer.Count);
            }
            fs.Flush();
            fs.Close();
        }
        
        public TempFileSaver(int bufferSize,int elementCount)
        {
            this.buffer = new List<byte>() { Capacity = bufferSize };
            this.bufferSize = bufferSize;
            this.tempPath = Path.GetTempFileName();
            fs = new FileStream(this.tempPath, FileMode.Create);
            this.buffer.AddRange(BitConverter.GetBytes(elementCount));
        }


        public TempFileSaver()
        {

        }
        private void WriteMeshElement(MeshElement elem)
        {
            var elemData = ConvertElement2Bytes(elem, out var size);
            this.buffer.AddRange(elemData);
            if(this.buffer .Count >this.bufferSize)
            {
                fs.Write (this.buffer.ToArray(),0,this.buffer.Count);
                fs.Flush();
                this.buffer.Clear();
                this.buffer.Capacity = this.bufferSize;
            }
            
        }
        /// <summary>
        /// Write mesh element to temp file
        /// </summary>
        /// <param name="elem">the element to write</param>
        /// <param name="calculateElementSurfaceArea">
        /// Whether or not calculate Element Box Area
        /// </param>
        public void WriteMeshElement(MeshElement elem,bool calculateElementSurfaceArea)
        {
            if(calculateElementSurfaceArea)
            {
                //get element box area
                double boxArea = elem.GetTriangleLength();
                this.MeshBoxAreas.Add(boxArea);
            }
            WriteMeshElement(elem);
        }


        /// <summary>
        /// Convert elem as bytes
        /// </summary>
        /// <param name="elem"></param>
        /// <param name="dataSize"></param>
        /// <returns></returns>
        private byte[] ConvertElement2Bytes(MeshElement elem,out int dataSize)
        {
            List<byte> elemData = new List<byte>();
            var ve = elem;
            //elemId
            //elemId
            string strId = ve.ElementId;
            var strByte = Encoding.Default.GetBytes(strId);
            var idLen = (byte)strByte.Length;
            elemData.Add(idLen);
            elemData.AddRange(strByte);
            //name
            string strNa = ve.Name;
            strByte = Encoding.Default.GetBytes(strNa);
            idLen = (byte)strByte.Length;
            elemData.Add(idLen);
            elemData.AddRange(strByte);
            //category
            string strCat = ve.Category;
            strByte = Encoding.Default.GetBytes(strCat);
            idLen = (byte)strByte.Length;
            elemData.Add(idLen);
            elemData.AddRange(strByte);
            //add solid data
            elemData.AddRange(BitConverter.GetBytes(ve.Solids.Count));
            foreach (var sld in ve.Solids)
            {
                //get vertices Count
                elemData.AddRange(BitConverter.GetBytes(sld.Vertices.Count));
                //get vertices
                foreach (var v in sld.Vertices)
                {
                    elemData.AddRange(BitConverter.GetBytes(v.X));
                    elemData.AddRange(BitConverter.GetBytes(v.Y));
                    elemData.AddRange(BitConverter.GetBytes(v.Z));
                }
                //get triangles
                elemData.AddRange(BitConverter.GetBytes(sld.Triangles.Count));
                foreach (var tri in sld.Triangles)
                {
                    for(int i=0;i<=2;i++)
                    {
                        elemData.AddRange(BitConverter.GetBytes(tri.VerticesIndex[i]));
                    }
                }
            }
            //get function
            elemData.AddRange(BitConverter.GetBytes(ve.IsSupportElem));
            elemData.AddRange(BitConverter.GetBytes(ve.IsActive));
            elemData.AddRange(BitConverter.GetBytes(ve.isTransport));
            elemData.TrimExcess();
            dataSize = elemData.Count;
            return elemData.ToArray(); ;
        }
        private  byte[] ConvertElement2Bytes(VoxelElement elem, out int dataSize)
        {
            List<byte> elemData = new List<byte>();
            var ve = elem;
            //elemId
            string strId = ve.ElementId;
            var strByte = Encoding.Default.GetBytes(strId);
            var idLen = (byte)strByte.Length;
            elemData.Add(idLen);
            elemData.AddRange(strByte);
            //name
            string strNa = ve.Name;
            strByte = Encoding.Default.GetBytes(strNa);
            idLen = (byte)strByte.Length;
            elemData.Add(idLen);
            elemData.AddRange(strByte);
            //category
            string strCat = ve.Category;
            strByte = Encoding.Default.GetBytes(strCat);
            idLen = (byte)strByte.Length;
            elemData.Add(idLen);
            elemData.AddRange(strByte);
            //add voxCount
            elemData.AddRange(BitConverter.GetBytes(ve.Voxels.Count));
            //add voxContent
            foreach (var vox in ve.Voxels)
            {
                List<byte> voxBytes = new List<byte>() { Capacity = 40 };
                voxBytes.AddRange(BitConverter.GetBytes(vox.ColIndex));
                voxBytes.AddRange(BitConverter.GetBytes(vox.RowIndex));
                voxBytes.AddRange(BitConverter.GetBytes(vox.BottomElevation));
                voxBytes.AddRange(BitConverter.GetBytes(vox.TopElevation));
                elemData.AddRange(voxBytes);
            }
            elemData.AddRange(BitConverter.GetBytes(ve.IsSupportElement));
            elemData.AddRange(BitConverter.GetBytes(ve.IsActive));
            elemData.AddRange(BitConverter.GetBytes(ve.IsTransportElement));
            elemData.TrimExcess();
            //insert elemDataLength
            dataSize = elemData.Count;
            return elemData.ToArray();
        }

        public  VoxelDocument ReadVoxelsFromTempFiles()
        {
            //1. Origin:24bytes;
            VoxelDocument result = new VoxelDocument();
            this.fs= new FileStream(tempPath, FileMode.Open);
            BinaryReader br = new BinaryReader(fs, Encoding.Default);
            try
            {
                long dataSizeRead = 0;
                double[] dblVec3 = new double[3];
                for (int i = 0; i <= 2; i++)
                {
                    dblVec3[i] = BitConverter.ToDouble(br.ReadBytes(8), 0);
                    dataSizeRead += 8;
                }
                Vec3 origin = new Vec3(dblVec3[0], dblVec3[1], dblVec3[2]);
                result.Origin = origin;
                //read voxel size
                result.VoxelSize = br.ReadDouble();
                dataSizeRead += 8;
                //read elements
                while(dataSizeRead<fs.Length)
                {
                    var voxElem = new VoxelElement();
                    voxElem.Voxels = new List<Voxel>();
                    result.Elements.Add(voxElem);
                    //Id
                    byte strLen = br.ReadByte();
                    var stringByte = new byte[strLen];
                    for (int j = 0; j < strLen; j++)
                    {
                        stringByte[j] = br.ReadByte();
                    }
                    dataSizeRead += strLen+1;
                    var elemId = Encoding.Default.GetString(stringByte);
                    voxElem.ElementId = elemId;
                    //Name
                    strLen = br.ReadByte();
                    stringByte = new byte[strLen];
                    for (int j = 0; j < strLen; j++)
                    {
                        stringByte[j] = br.ReadByte();
                    }
                    var name = Encoding.Default.GetString(stringByte);
                    voxElem.Name = name;
                    dataSizeRead += strLen + 1;
                    //Category
                    strLen = br.ReadByte();
                    stringByte = new byte[strLen];
                    for (int j = 0; j < strLen; j++)
                    {
                        stringByte[j] = br.ReadByte();
                    }
                    var cat = Encoding.Default.GetString(stringByte);
                    voxElem.Category = cat;
                    dataSizeRead += strLen + 1;
                    //vox
                    int voxCount = BitConverter.ToInt32(br.ReadBytes(4), 0);
                    dataSizeRead += 4;
                    List<Voxel> voxels = new List<Voxel>() { Capacity = voxCount };
                    for (int j = 0; j < voxCount; j++)
                    {
                        //3.voxels, per including  Col(4 bytes) Row(4 bytes) bottom Elevation(4 bytes) top elevation(4 bytes)
                        var vox = new Voxel();
                        //read col
                        vox.ColIndex = BitConverter.ToInt32(br.ReadBytes(4), 0);
                        dataSizeRead += 4;
                        //read row
                        vox.RowIndex = BitConverter.ToInt32(br.ReadBytes(4), 0);
                        dataSizeRead += 4;
                        //read btmElev
                        vox.BottomElevation = BitConverter.ToDouble(br.ReadBytes(8), 0);
                        dataSizeRead += 8;
                        //read topElev
                        vox.TopElevation = BitConverter.ToDouble(br.ReadBytes(8), 0);
                        dataSizeRead += 8;
                        vox.TopAdjVoxels = new Voxel[4];
                        voxels.Add(vox);
                    }
                    voxElem.Voxels = voxels;
                    voxElem.IsSupportElement = br.ReadBoolean();
                    voxElem.IsActive = br.ReadBoolean();
                    voxElem.IsTransportElement = br.ReadBoolean();
                    dataSizeRead += 3;
                }
                return result;
            }
            catch(Exception ex)
            {
                return null;
            }
            finally
            {
                br.Close();
                fs.Dispose();
                File.Delete(tempPath);
            }
        }

        public bool SaveAs(string newPath,bool replaceTempFile)
        {
            try
            {
                File.Copy(this.tempPath, newPath, true);
                if(replaceTempFile)
                {
                    File.Delete(this.tempPath);
                    this.tempPath = newPath;
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace);
                return false;
            }
        }
        /// <summary>
        /// Read mesh element from temp files
        /// </summary>
        /// <returns></returns>
        public IEnumerable<MeshElement> ReadMeshElementFromTempFile()
        {
            foreach (var me in ReadMeshElement(this.tempPath,this.DeleteAfterRead))
            {
                yield return me;
            }
        }
        /// <summary>
        /// Test parallel output
        /// </summary>
        /// <param name="groupNumber"></param>
        /// <returns></returns>
        public IEnumerable<MeshElementCollection> ReadMeshElementsFromTempFile(int groupNumber)
        {
            int numElemRead = 0;
            double dblTotalArea = this.MeshBoxAreas.Sum();
            double dblGroupMaxArea = dblTotalArea / groupNumber;
            double dblAreaInGroup = 0;
            List<MeshElement> elemGroup = new List<MeshElement>();
            foreach (var me in ReadMeshElement(this.tempPath, this.DeleteAfterRead))
            {
                var elemArea = this.MeshBoxAreas[numElemRead];
                elemGroup.Add(me);
                dblAreaInGroup += elemArea;
                if(dblAreaInGroup>dblGroupMaxArea)  
                {
                    yield return new MeshElementCollection(dblAreaInGroup,elemGroup);
                    //reset element group
                    elemGroup = new List<MeshElement>();
                    dblAreaInGroup = 0;
                }
                numElemRead += 1;
            }
            //output the remaining element
            if(elemGroup.Count > 0)
            {
                yield return new MeshElementCollection(dblAreaInGroup, elemGroup) ;
            }
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public int GetApproximateElementNumber()
        {
            this.fs = new FileStream(this.tempPath, FileMode.Open);
            BinaryReader br = new BinaryReader(fs, Encoding.Default);
            try
            {
                //get element count
                int numElems = br.ReadInt32();
                return numElems;
            }
            finally
            {
                br.Close();
                fs.Close();
            }
        }
        
        public IEnumerable<MeshElement> ReadMeshElement(string filePath,bool deleteAfterRead)
        {
            this.fs = new FileStream(filePath, FileMode.Open);
            BinaryReader br = new BinaryReader(fs, Encoding.Default);
            FileInfo fInfo = new FileInfo(filePath);
            var fileSize = fInfo.Length;
            long byteLoaded = 0;
            try
            {
                //get element count
                int numElems = br.ReadInt32();
                byteLoaded += 4;
                //for (int i = 0; i < numElems; i++)
                while (byteLoaded < fileSize)
                {
                    
                    MeshElement me = new MeshElement();
                    //read element
                    //Id
                    byte strLen = br.ReadByte();
                    var stringByte = new byte[strLen];
                    for (int j = 0; j < strLen; j++)
                    {
                        stringByte[j] = br.ReadByte();
                    }
                    byteLoaded += 1 + stringByte.Length;
                   
                    var elemId = Encoding.Default.GetString(stringByte);
                    me.ElementId = elemId;
                    //Name
                    strLen = br.ReadByte();
                    stringByte = new byte[strLen];
                    for (int j = 0; j < strLen; j++)
                    {
                        stringByte[j] = br.ReadByte();
                    }
                    var name = Encoding.Default.GetString(stringByte);
                    me.Name = name;
                    byteLoaded += 1 + stringByte.Length;
                    //Category
                    strLen = br.ReadByte();
                    stringByte = new byte[strLen];
                    for (int j = 0; j < strLen; j++)
                    {
                        stringByte[j] = br.ReadByte();
                    }
                    var cat = Encoding.Default.GetString(stringByte);
                    me.Category = cat;
                    byteLoaded += 1 + stringByte.Length;
                    //solid
                    int numSlds = br.ReadInt32();
                    byteLoaded += 4;
                    for (int j = 0; j < numSlds; j++)
                    {
                        MeshSolid sld = new MeshSolid(me, new List<Vec3>(), new List<int>());
                        me.Solids.Add(sld);
                        int numVertices = br.ReadInt32();
                        byteLoaded += 4;
                       
                        sld.Vertices.Capacity = numVertices;
                        for (int k = 0; k < numVertices; k++)
                        {
                            double x = br.ReadDouble();
                            double y = br.ReadDouble();
                            double z = br.ReadDouble();
                            byteLoaded += 24;
                           
                            Vec3 v3 = new Vec3(x, y, z);
                            sld.Vertices.Add(v3);
                        }
                        //restore triangle
                        int numTriangles = br.ReadInt32();
                        byteLoaded += 4;
                        
                        sld.Triangles.Capacity = numTriangles;
                        for (int k = 0; k < numTriangles; k++)
                        {
                            MeshTriangle tri = new MeshTriangle(sld, new int[3]);
                            sld.Triangles.Add(tri);
                            for (int t = 0; t <= 2; t++)
                            {
                                tri.VerticesIndex[t] = br.ReadInt32();
                                byteLoaded += 4;
                               
                            }

                        }
                    }
                    me.IsSupportElem = br.ReadBoolean();
                    me.IsActive = br.ReadBoolean();
                    me.isTransport = br.ReadBoolean();
                    byteLoaded += 3;
                    yield return me;
                }
            }
           
            finally
            {
                
                br.Close();
                fs.Close();
                if (deleteAfterRead)
                    Terminate(filePath);
            }

        }
      


        public void Terminate()
        {
            
            if (!string.IsNullOrEmpty (tempPath) && File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        public void Terminate( string filePath)
        {
           
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
        
    public class VoxelDocument
    {
        public Vec3 Origin { get; set; }
        public double VoxelSize { get; set; }
        public List<VoxelElement> Elements { get; set; } = new List<VoxelElement>();
       
       
        public void GenereateIndoorVoxelMaps(double strideHeight)
        {
            
            //generate accessible region

        }
    }
    public class VoxelElement
    {
        public string ElementId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public List<Voxel> Voxels { get; set; }
        public bool IsSupportElement { get; set; } = false;
        public bool IsObstructElement { get; set; } = true;
        public bool IsTransportElement { get; set; } = false;
        public bool IsActive { get; set; } = true;
       
        public VoxelElement() { }
        public VoxelElement(VoxelDocument doc, MeshElement meshElement,bool fillVoxels)
        {
            List<Voxel> vox = new List<Voxel>();
            var solids = meshElement.Solids;
            var origin = doc.Origin;
            var voxSize = doc.VoxelSize;
            //calcualte gridPointnumber
            int gpNumberAll = 0;
            foreach (var sld in solids)
            {
                sld.SetAllGridPoints(origin, voxSize,out int gpNumber);
                gpNumberAll += gpNumber;
            } 
            vox.Capacity = gpNumberAll / 3;
            foreach (var sld in solids)
            {
                vox.AddRange(sld.GenerateVoxels(fillVoxels));
            }
            vox.TrimExcess();
            this.IsActive = meshElement.IsActive;
            this.IsSupportElement = meshElement.IsSupportElem;
            this.IsTransportElement = meshElement.isTransport;
            this.Voxels = vox;
            this.ElementId = meshElement.ElementId;
            this.Category = meshElement.Category;
            this.Name = meshElement.Name;
            this.IsTransportElement = meshElement.isTransport;
        }
        public VoxelElement(VoxelDocument doc, MeshElement meshElement,bool MergeIntersecting, bool fillVoxels)
        {
            List<Voxel> vox = new List<Voxel>();
            var solids = meshElement.Solids;
            var origin = doc.Origin;
            var voxSize = doc.VoxelSize;
            //calcualte gridPointnumber
            int gpNumberAll = 0;
            foreach (var sld in solids)
            {
                sld.SetAllGridPoints(origin, voxSize, out int gpNumber);
                gpNumberAll += gpNumber;
            }
            vox.Capacity = gpNumberAll / 3;
            foreach (var sld in solids)
            {
                vox.AddRange(sld.GenerateVoxels(MergeIntersecting, fillVoxels));
            }
            vox.TrimExcess();
            this.IsActive = meshElement.IsActive;
            this.IsSupportElement = meshElement.IsSupportElem;
            this.IsTransportElement = meshElement.isTransport;
            this.Voxels = vox;
            this.ElementId = meshElement.ElementId;
            this.Category = meshElement.Category;
            this.Name = meshElement.Name;
            this.IsTransportElement = meshElement.isTransport;
        }

        
        /// <summary>
        /// Create an mewh Element with gridPts but no voxels
        /// </summary>
        /// <param name="meshElement"></param>
        /// <param name="voxSize"></param>
        public VoxelElement(MeshElement meshElement, double voxSize)
        {
            List<Voxel> vox = new List<Voxel>();
            var solids = meshElement.Solids;
            var origin = Vec3.Zero;
            //calcualte gridPointnumber
            int gpNumberAll = 0;
            foreach (var sld in solids)
            {
                gpNumberAll += sld.GenerateGridPoints_Parallel(origin, voxSize); 
            }
            vox.Capacity = gpNumberAll;
            this.Voxels = vox;
            this.IsActive = meshElement.IsActive;
            this.IsSupportElement = meshElement.IsSupportElem;
            this.IsTransportElement = meshElement.isTransport;
            this.ElementId = meshElement.ElementId;
            this.Category = meshElement.Category;
            this.Name = meshElement.Name;
            this.IsTransportElement = meshElement.isTransport;
        }

        public void Voxelize(MeshElement meshElement,double voxSize,bool fillVoxels)
        {
            foreach (var sld in meshElement.Solids)
            {
                this.Voxels.AddRange(sld.GenerateVoxels_Parallel(fillVoxels));
            }
            
        }
        /// <summary>
        /// Load voxels, the origin is at (0,0,0)
        /// </summary>
        /// <param name="voxelSize"></param>
        /// <param name="meshElement"></param>
        /// <param name="fillVoxels"></param>
    }


    public class CompressedVoxelDocument
    {
        public Dictionary<int,int> VoxelHight { get; set; }
        public List<CellIndex3D> VoxelScale { get; set; }
        public double VoxelSize { get; set; }
        public Vec3 Origin { get; set; }
        public List<CompressedVoxelElement> Elements { get; set; }

        public CompressedVoxelDocument()
        {

        }
        public CompressedVoxelDocument( ref Dictionary <CellIndex3D,int> scales, VoxelDocument voxDoc)
        {
            this.VoxelSize = voxDoc.VoxelSize;
            this.Origin = voxDoc.Origin;
            Dictionary<int, int> voxHeigtMM_VoxelIndex = new Dictionary<int, int>();
            this.Elements = new List<CompressedVoxelElement>();
            foreach (var ve in voxDoc.Elements)
            {
                var rects= LEGOVoxelTool.CompressVoxels(ref scales, ve);
                var compElem = new CompressedVoxelElement(ve, rects);
                Elements.Add(compElem);
            }
            this.VoxelHight = voxHeigtMM_VoxelIndex;
        }
        
    }


    public class CompressedVoxelElement
    {
        public string ElementId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public List<VoxelRectangle> VoxelRectangles { get; set; }
        public bool IsSupportElement { get; set; } = false;
        public bool IsObstructElement { get; set; } = true;
        public bool IsTransportElement { get; set; } = false;
        public bool IsActive { get; set; } = true;
        public int VoxelIndex { get; set; } = -1;
        public CompressedVoxelElement()
        {

        }
        public CompressedVoxelElement(VoxelElement elem,List<VoxelRectangle> rect)
        {
            ElementId = elem.ElementId;
            Name = elem.Name;
            Category = elem.Category;
            IsActive = elem.IsActive;
            IsSupportElement = elem.IsSupportElement;
            IsTransportElement=elem.IsTransportElement;
            IsObstructElement = elem.IsObstructElement;
            VoxelRectangles = rect;
        }

        public VoxelElement ToVoxelElement(CompressedVoxelDocument doc,double unitConvertRatio)
        {
            VoxelElement ve = new VoxelElement();
            ve.IsSupportElement = IsSupportElement;
            ve.IsTransportElement = IsTransportElement;
            ve.IsActive = IsActive;
            ve.IsObstructElement = IsObstructElement;
            ve.Category = Category;
            ve.ElementId = ElementId;
            ve.Voxels = new List<Voxel>();
            foreach (var rect in this.VoxelRectangles)
            {
                ve.Voxels.AddRange ( rect.ToVoxels(doc,unitConvertRatio));
            }
            return ve;
        }

        internal void Get_BoundingBox(CompressedVoxelDocument vdoc,  out CellIndex min, out CellIndex max, out double bottomElev, out double topElev)
        {
            int colMax = int.MinValue;
            int rowMax = int.MinValue;
            int colMin = int.MaxValue;
            int rowMin = int.MaxValue;
            int zMin = int.MaxValue;
            int zMax = int.MinValue;
            foreach (var rect in this.VoxelRectangles)
            {
               var cix3D= rect.Get_Scale(vdoc.VoxelScale);
                var colMaxTemp =rect.Start.Col+ cix3D.Col;
                var rowMaxTemp =rect.Start .Row + cix3D.Row;
                var zMaxTemp =rect.BottomElevation+ cix3D.Layer;

                var colMinTemp = rect.Start.Col;
                var rowMinTemp = rect.Start.Row;
                var zMinTemp = rect.BottomElevation;
                colMax = Math.Max(colMaxTemp, colMax);
                rowMax = Math.Max(rowMaxTemp, rowMax);
                colMin = Math.Min(colMinTemp, colMin);
                rowMin = Math.Min(rowMinTemp, rowMin);
                zMin = Math.Min(zMinTemp, zMin);
                zMax = Math.Max(zMaxTemp, zMax);
            }
            min = new CellIndex(colMin, rowMin);
            max = new CellIndex(colMax, rowMax);
            bottomElev = zMin / 304.8;
            topElev = zMax / 304.8;
            //throw new NotImplementedException();
        }
    }
    public class Voxel
    {
        public MeshElement Host { get; set; }
        public int ColIndex { get; set; }
        public int RowIndex { get; set; }
        public double BottomElevation { get; set; }
        public double TopElevation { get; set; } 
        public Voxel TopVoxel { get; set; }
        public Voxel BottomVoxel { get; set; }
        public Voxel[] BottomAdjVoxels { get; set; }
        public Voxel[] TopAdjVoxels { get; set; }
        // This property is valid for odd voxels
        public VoxelType VoxType { get; set; } = VoxelType.Common;
        public List<Voxel> LinkedSupportVoxels { get; set; }=new List<Voxel>();
        public List<Voxel> LinkedTransportVoxels { get; set; } = new List<Voxel>();
        public Voxel Parent { get; set; }
        public bool IsBoundaryVoxel { get; set; } = false;
        public bool IsSupportVoxel { get; set; } = true;
        public bool IsActive { get; set; } = true;
        public bool Navigable { get; set; } = true;

        public bool[] ForbiddenDirections_Bottom { get; set; }
        public bool[] ForbiddenDirections_Top { get; set; }
        //this property is used for debug
        //remove after release
        public int Index { get; set; }

        public int BottomActivater { get; set; } = -1;
        public int TopActivater { get; set; } = -1;
        public int BoundaryActivater { get; set; } = -1;
        public Voxel(MeshElement host, int col, int row, double btmElev,double topElev)
        {
            Host = host;
            ColIndex = col;
            RowIndex = row;
            BottomElevation = btmElev;
            TopElevation = topElev;
            BottomVoxel = null;
            TopVoxel = null;
            BottomAdjVoxels=new Voxel[4];
            TopAdjVoxels = new Voxel[4];
            Parent = this;
        }
        public Voxel()
        {

        }
        public bool BottomOutside { get; set; } = false;
        public bool TopOutside { get; set; } = false;
        public AccessibleRegion OwnerRegion { get; internal set; }

        public VoxelRange GetLowerGapRange()
        {
            //obtain vox Button Gap
            double dblBottomGapSt = double.MinValue;
            double dblBottomGapEd = this.BottomElevation;
            if(BottomVoxel!=null)
            {
                dblBottomGapSt = BottomVoxel.TopElevation;
            }
            return new VoxelRange(dblBottomGapSt, dblBottomGapEd);
            
        }
        public VoxelRange GetUpperGapRange()
        {
            //obtan vox TopGap
            double dblTopGapSt = TopElevation;
            double dblTopGapEd = double.MaxValue;
            if (TopVoxel != null)
            {
                dblTopGapEd = TopVoxel.BottomElevation;
            }
            return new VoxelRange(dblTopGapSt, dblTopGapEd);
        }
        //Used for comparing F
        
        public IEnumerable<Voxel> GenerateSmallerVoxel(Vec3 origin, double voxelHeight)
        {
            var voxBtm = this.BottomElevation;
            var voxTop = this.TopElevation;
            int layerSt= (int) Math.Floor( Math.Round ( (voxBtm - origin.Z) / voxelHeight,4));
            int layerEd = (int)Math.Ceiling(Math.Round((voxTop - origin.Z) / voxelHeight, 4)) - 1;
            for(int layer =layerSt;layer <=layerEd;layer++)
            {
                var smallVoxel = new Voxel(this.Host, this.ColIndex, this.RowIndex, layer * voxelHeight + origin.Z, (layer + 1) * voxelHeight + origin.Z);
                yield return smallVoxel;
            }
        }
    }

    public class VoxelRectangle
    {
        /// <summary>
        /// index used for generating accessible rectangles
        /// </summary>
        public int Index { get; set; }
        /// <summary>
        /// bottom elevation in millimeter
        /// </summary>
        public int BottomElevation { get; set; }
        public CellIndex Start { get; set; }
        /// <summary>
        /// first: col scale; second:Rogetw scale，third：height
        /// </summary>
        public int ScaleIndex { get; set; }
        public CellIndex3D  Get_Scale(List<CellIndex3D> scaleList)
        {
            return scaleList[this.ScaleIndex];
        }
        public VoxelRectangle()
        {
           
            this.Start = new CellIndex(0,0);
            
        }
       
        /// <summary>
        /// Construct a rectangle
        /// </summary>
        /// <param name="voxelIndex">voxel height index</param>
        /// <param name="bottomElev_MM">rectange bottom elevation</param>
        /// <param name="start">start voxel index</param>
        /// <param name="scale">scale,col-row-heightIndex</param>
        public VoxelRectangle (int scaleIndex,int bottomElev_MM,CellIndex start,CellIndex scale)
        {
            this.BottomElevation = bottomElev_MM;
            this.Start = start;
            this.ScaleIndex = scaleIndex;
        }

        public IEnumerable<CellIndex> Get_CellIndexes(CellIndex3D scale)
        {
            
            int colScale = scale.Col;
            int rowScale=scale.Row; 
            for(int col=0;col<=colScale;col++)
            {
                for(int row=0;row<=rowScale; row++)
                {
                    yield return (Start + new CellIndex(col, row));
                }
            }
        }
        /// <summary>
        /// convert rectangle to voxels
        /// </summary>
        /// <param name="doc"> compressed doc</param>
        /// <param name="unitConversionRatio">the ratio from mm to the internal unit of the software</param>
        /// <returns>voxels</returns>
        public IEnumerable<Voxel> ToVoxels(CompressedVoxelDocument doc, double unitConversionRatio)
        {
            var scale = doc.VoxelScale[this.ScaleIndex];
            var heightMM = scale.Layer;
            double dblActualHeight = heightMM * unitConversionRatio;
            double dblActualBottomElev = this.BottomElevation * unitConversionRatio;
            double dblActualTopElev = this.BottomElevation * unitConversionRatio + dblActualHeight;
            foreach (var cix in this.Get_CellIndexes(scale))
            {
                Voxel voxNew = new Voxel() { BottomElevation = dblActualBottomElev, TopElevation = dblActualTopElev, ColIndex=cix.Col,RowIndex=cix.Row };
                yield return voxNew;  
            }
        }

        internal void Get_BoundingBox(CompressedVoxelDocument compVoxelDoc, out CellIndex min, out CellIndex max, out double bottomElev, out double topElev)
        {
            var rect = this;
            int colMax = int.MinValue;
            int rowMax = int.MinValue;
            int colMin = int.MaxValue;
            int rowMin = int.MaxValue;
            int zMin = int.MaxValue;
            int zMax = int.MinValue;
            var cix3D = rect.Get_Scale(compVoxelDoc.VoxelScale);
            var colMaxTemp = rect.Start.Col + cix3D.Col;
            var rowMaxTemp = rect.Start.Row + cix3D.Row;
            var zMaxTemp = rect.BottomElevation + cix3D.Layer;

            var colMinTemp = rect.Start.Col;
            var rowMinTemp = rect.Start.Row;
            var zMinTemp = rect.BottomElevation;
            colMax = Math.Max(colMaxTemp, colMax);
            rowMax = Math.Max(rowMaxTemp, rowMax);
            colMin = Math.Min(colMinTemp, colMin);
            rowMin = Math.Min(rowMinTemp, rowMin);
            zMin = Math.Min(zMinTemp, zMin);
            zMax = Math.Max(zMaxTemp, zMax);
            min = new CellIndex(colMin, rowMin);
            max = new CellIndex(colMax, rowMax);
            bottomElev = zMin / 304.8;
            topElev = zMax / 304.8;
        }
    }
    public enum VoxelType
    {
        Common=0,
        Odd=1,

    }
    
    
    public class VoxelRange
    {
        public double StartElevation { get; set; }
        public double EndElevation { get; set; }
        public double GetElevationRange()
        {
            return EndElevation - StartElevation;
        }
        public VoxelRange(double startElev,double endElev)
        {
            StartElevation = startElev;
            EndElevation = endElev;
        }
        public bool Intersect(VoxelRange other,out VoxelRange intersectionResult)
        {
            var endElev = Math.Min(this.EndElevation, other.EndElevation);
            var startElev = Math.Max(this.StartElevation, other.StartElevation);
            if(Math.Round (endElev -startElev,4)>0)
            {
                intersectionResult = new VoxelRange(startElev, endElev);
                return true;
            }
            else
            {
                intersectionResult = null;
                return false;
            }
            
        }
        public bool Intersect(VoxelRange other)
        {
            var stThis = this.StartElevation;
            var edThis = this.EndElevation;
            var stOther = other.StartElevation;
            var edOther = other.EndElevation;
            var dis0 = edThis - stOther;
            var dis1 = edOther - stThis;
            if(Math.Round (dis0,4)>0 && Math.Round (dis1,4)>0)
            {
                return true;
            }
            else
            {
                return false;
            } 
                
        }
    }
    public class GridPoint
    {
        public int Column { get; set; }
        public int Row { get; set; }
        public double Z { get; set; }

        public Vec2 XY { get; set; }


        public GridPointType GridType { get; set; }
        public GridPoint(double x,double y,int column, int row, double z, GridPointType gridType)
        {
            Column = column;
            Row = row;
            Z = z;
            GridType = gridType;
            this.XY =new Vec2(x,y);
        }
        public GridPoint(Vec3 pt,Vec3 origin,double voxSize)
        {
            this.XY = new Vec2(pt.X, pt.Y);
            var dblCol = Math.Round((pt.X - origin.X) / voxSize, 4);
            var dblRow = Math.Round((pt.Y - origin.Y) / voxSize, 4);
            this.Column =(int) Math.Floor(dblCol);
            this.Row = (int)Math.Floor(dblRow);
            this.Z = pt.Z;
            if(dblCol !=this.Column && dblRow !=this.Row)
            {
                this.GridType = GridPointType.VGP;
            }
            else if(dblCol ==this.Column && dblRow !=this.Row)
            {
                this.GridType = GridPointType.CGP;
            }
            else if(dblCol !=this.Column && dblRow ==this.Row)
            {
                this.GridType = GridPointType.RGP;
            }
            else
            {
                this.GridType = GridPointType.IGP;
            }
        }

        public Vec3 GetCoordinates(Vec3 origin,double voxelSize)
        {
            if(this.XY !=null)
            {
                return new Vec3(this.XY.U, this.XY.V, this.Z);
            }
            else
            {
                var x = origin.X + this.Column * voxelSize;
                var y = origin.Y + this.Row * voxelSize;
                var z = this.Z;
                return new Vec3(x, y, z); 
            }
        }
    }

   

    public enum GridPointType
    {
        VGP=0,
        CGP=1,
        RGP=2,
        IGP=3,
    }
    #endregion
    #region Nav Class
    public static class PathPlanningTool
    {
        public static AccessibeDocument LoadAccessibleRegionDocument(string filePath)
        {
            //1. Origin:24bytes;
            AccessibeDocument result = new AccessibeDocument();
            FileStream fs = new FileStream(filePath, FileMode.Open);
            BinaryReader br = new BinaryReader(fs, Encoding.Default);
            double[] dblVec3 = new double[3];
            for (int i = 0; i <= 2; i++)
            {
                dblVec3[i] = BitConverter.ToDouble(br.ReadBytes(8), 0);
            }
            Vec3 origin = new Vec3(dblVec3[0], dblVec3[1], dblVec3[2]);
            result.Origin = origin;
            //vox size
            double dblVoxSize = BitConverter.ToDouble(br.ReadBytes(8), 0);
            result.VoxelSize = dblVoxSize;
            //Load accessible regions
            int numARs = br.ReadInt32();
            List<AccessibleRegion> regions = new List<AccessibleRegion>() { Capacity = numARs };
            for (int i = 0; i < numARs; i++)
            {
                AccessibleRegion ar = new AccessibleRegion();
                result.Regions.Add(ar);
                //read rectangles
                int numRects = br.ReadInt32();
                ar.Rectangles = new List<AccessibleRectangle>() { Capacity = numRects };
                List<List<int>> lisRect_AdjRel = new List<List<int>>() { Capacity = numRects };
                for (int j = 0; j < numRects; j++)
                {
                    AccessibleRectangle rect = new AccessibleRectangle();
                    ar.AppendRectangle(rect);
                    rect.Index = j;
                    //read min and max
                    CellIndex cixMin = new CellIndex(br.ReadInt32(), br.ReadInt32());
                    CellIndex cixMax = new CellIndex(br.ReadInt32(), br.ReadInt32());
                    rect.Min = cixMin;
                    rect.Max = cixMax;
                    //read elev
                    rect.Elevation = br.ReadDouble();
                    //read adjRel
                    int numNeighbours = br.ReadInt32();
                    List<int> neighbourIndexes = new List<int>() { Capacity = numNeighbours };
                    for (int k = 0; k <= numNeighbours - 1; k++)
                    {
                        neighbourIndexes.Add(br.ReadInt32());
                    }
                    lisRect_AdjRel.Add(neighbourIndexes);
                }
                //restore rect neighbours
                for (int j = 0; j < numRects; j++)
                {
                    var neighbourIds = lisRect_AdjRel[j];
                    var rect = ar.Rectangles[j];
                    rect.AdjacentRectangles = new List<AccessibleRectangle>() { Capacity = neighbourIds.Count };
                    foreach (var neighbourIdx in neighbourIds)
                    {
                        rect.AdjacentRectangles.Add(ar.Rectangles[neighbourIdx]);
                    }
                }
            }
            return result;
        }
        public static bool PathPlanning(Vec3 start,Vec3 target,AccessibeDocument doc,out List<Vec3> path )
        {
            path = new List<Vec3>();
            var startPt = FindAccessibleRectBelow(start, doc, out var rectStart);
            var endPt = FindAccessibleRectBelow(target, doc, out var rectEnd);
            if(rectStart.Owner == rectEnd.Owner)
            {
                if (!doc.GateGenerated)
                {
                    doc.GenerateGates();
                }
                //reset result
                doc.ResetGate();
                //path planning
                //init first gate
                List<AccessibleGate> gateOpen = new List<AccessibleGate>();
                if (rectStart != null && rectEnd != null)
                {
                    foreach (var gate in rectStart.Gates)
                    {
                        gate.G = gate.DistanceTo(startPt, out var gateWP);
                        gate.H = (endPt - gateWP).GetLength();
                        gate.F = gate.G + gate.H;
                        gate.LocationPoints = gateWP;
                        gate.IsOpen = true;
                        gateOpen.Add(gate);
                    }
                }
                //using A* to find gates
                AccessibleGate gateCur = null;
                while (gateOpen.Count > 0)
                {
                    //find the path
                    gateCur = GetMin(ref gateOpen);
                    gateCur.IsClose = true;
                    Vec3 lcPtCur = gateCur.LocationPoints;
                    var rectCur = gateCur.RectanglesFrom;
                    //get the adjacent gate of current gate
                    List<AccessibleGate> gatesAdj = new List<AccessibleGate>();
                    gatesAdj.Add(gateCur.GateTo);
                    foreach (var gate in rectCur.Gates)
                    {
                        if (gate.IsClose == false)
                        {
                            gatesAdj.Add(gate);
                        }
                    }

                    if (rectCur == rectEnd)
                    {
                        break;
                    }
                    foreach (var gateAdj in gatesAdj)
                    {
                        if (!gateAdj.IsClose)
                        {
                            double gAdj = gateCur.G + gateAdj.DistanceTo(lcPtCur, out var gateWP);
                            if (gateAdj.G > gAdj)
                            {
                                gateAdj.G = gAdj;
                                gateAdj.LocationPoints = gateWP;
                                gateAdj.H = (endPt - gateWP).GetLength();
                                gateAdj.F = gateAdj.G + gateAdj.H;
                                gateAdj.Previous = gateCur;
                            }
                            if (!gateAdj.IsOpen)
                            {
                                gateAdj.IsOpen = true;
                                gateOpen.Add(gateAdj);
                            }
                        }
                    }
                }
                //generate path
                path.Add(endPt);
                while (gateCur != null)
                {
                    path.Add(gateCur.LocationPoints);
                    gateCur = gateCur.Previous;
                }
                path.Add(startPt);
                return true;
            }
            else
            {
                return false;
            }
           
        }
        private static AccessibleGate GetMin(ref List<AccessibleGate> gateOpen)
        {
            double dblF = double.MaxValue;
            AccessibleGate result = null;
            int resultIndex = 0;
            int pt = 0;
            foreach (var item in gateOpen)
            {
                if(item.F<dblF)
                {
                    dblF = item.F;
                    result = item;
                    resultIndex = pt;
                }
                pt += 1;
            }
            if(resultIndex!=gateOpen.Count-1) //pt is not the last, move it to gateOpen[pt] and then remove it
            {
                var gateLast = gateOpen[gateOpen.Count -1];
                gateOpen[resultIndex] = gateLast;
            }
            gateOpen.RemoveAt(gateOpen.Count - 1);

            return result;
        }
        private static Vec3 FindAccessibleRectBelow(Vec3 pt, AccessibeDocument doc,out AccessibleRectangle rect)
        {
            Vec3 result = null;
            double dblElev = double.MinValue;
            rect = null;
            foreach (var rng in doc.Regions)
            {
                foreach (var r in rng.Rectangles)
                {
                    double xMin = r.Min.Col * doc.VoxelSize + doc.Origin.X;
                    double yMin=r.Min.Row *doc.VoxelSize + doc.Origin.Y;
                    double xMax = (r.Max.Col + 1) * doc.VoxelSize + doc.Origin.X;
                    double yMax=(r.Max.Row +1) * doc.VoxelSize + doc.Origin.Y;
                    if(pt.Z >=r.Elevation && pt.X >=xMin && pt.X <=xMax && pt.Y >=yMin && pt.Y <=yMax)
                    {
                        if(r.Elevation >dblElev)
                        {
                            dblElev = r.Elevation;
                            result = new Vec3(pt.X, pt.Y, dblElev);
                            rect = r;
                        }
                    }
                }
            }
            return result;
        }
    }

    public class AccessibeDocument
    {
        public Vec3 Origin { get; set; }
        public double VoxelSize { get; set; }
        public List<AccessibleRegion> Regions { get; set; }
        public bool GateGenerated { get; private set; }

        public double GetArea(AreaUnit unit)
        {
            double dblAreaSquareFeet = 0;
            foreach (var rng in this.Regions)
            {
                foreach (var rect in rng.Rectangles)
                {
                    CellIndex cixScale = rect.Max - rect.Min + new CellIndex(1, 1);
                    double width = cixScale.Col * this.VoxelSize;
                    double height = cixScale.Row * this.VoxelSize;
                    double area=width* height;
                    dblAreaSquareFeet += area;
                }
            }
            switch(unit)
            {
                case AreaUnit.SquareMeter:
                    return dblAreaSquareFeet * Math.Pow(0.3048, 2);
                    break;
                default:
                    return dblAreaSquareFeet;
            }
        }
        public enum AreaUnit
        {
            SqureFeet=0,
            SquareMeter=1,
        }
        public AccessibeDocument()
        {
            this.Origin = Vec3.Zero;
            this.VoxelSize = 0;
            this.Regions = new List<AccessibleRegion>();
        }
        /// <summary>
        /// Generate accessible gates for each rectangles.
        /// </summary>
        public void GenerateGates()
        {
            foreach (var rng in this.Regions)
            {
                foreach (var rect in rng.Rectangles)
                {
                    rect.GenerateGates(this.Origin, this.VoxelSize);
                }
            }
            this.GateGenerated = true;
        }
        /// <summary>
        /// Clear generated way points, call it before a new navigation start
        /// </summary>
        public void ResetGate()
        {
            foreach (var rng in this.Regions)
            {
                foreach (var rect in rng.Rectangles)
                {
                    foreach (var gate in rect.Gates)
                    {
                        gate.LocationPoints = null;
                        gate.G = double.MaxValue;
                        gate.H = double.MaxValue;
                        gate.F = double.MaxValue;
                        gate.Previous = null;
                        gate.IsClose = false;
                        gate.IsOpen = false;
                    }
                }
            }
        }
    }
    public class AccessibleRectangle
    {
        public double Elevation { get; set; }
        public CellIndex Min { get; set; }
        public CellIndex Max { get; set; }
        public List<AccessibleRectangle> AdjacentRectangles { get; set; } = new List<AccessibleRectangle>();
        public Voxel[,] Voxels { get; set; }
        public int Index { get; set; }
        public Vec3 LocationPoint { get; set; }
        
        //way pt of path planning
        public double G { get; set; }
        public double H { get; set; }
        public double F { get; set; }

        public bool IsOpen { get; set; } = false;
        public bool IsClosed { get; set; } = false;
        public bool WaypointGenerated { get; set; } = false;
        public AccessibleRectangle Previous { get; set; }
        //adj rects reaching the current rectangle
        public HashSet<int> AdjRectIdxReached { get; set; } = new HashSet<int>();
        public AccessibleRegion Owner { get; internal set; }

        public List<AccessibleGate> Gates { get; set; } = new List<AccessibleGate>();
        public List<AccessibleCell> GenerateCells()
        {
            var min=this.Min;
            var max = this.Max;
            List<AccessibleCell> cells = new List<AccessibleCell>();
            for(int col=min.Col;col<=max.Col;col++)
            {
                AccessibleCell cellLeft = null;
                for(int row=min.Row;row<=max.Row;row++ )
                {
                    var cell=new AccessibleCell() { Index=new CellIndex (col,row),Owner =this};
                    cell.Neighbours = new AccessibleCell[8];
                    if(cellLeft!=null)
                    {

                    }
                }
            }
            return cells;
        }

        public void GenerateGates(Vec3 origin, double voxelSize)
        {
            CellIndex min = this.Min;
            CellIndex max = this.Max+new CellIndex (1,1);
            foreach (var rectNear in this.AdjacentRectangles)
            {
                if(rectNear.Gates.Count !=rectNear.AdjacentRectangles.Count)
                {
                    var minNear = rectNear.Min;
                    var maxNear = rectNear.Max + new CellIndex(1, 1);
                    int colGateSt = Math.Max(minNear.Col, min.Col);
                    int  colGateEd = Math.Min(max.Col, maxNear.Col);
                    int rowGateSt = Math.Max(min.Row, minNear.Row);
                    int rowGateEd = Math.Min(max.Row, maxNear.Row);
                    double elevation = this.Elevation;
                    double elevationNear = rectNear.Elevation;
                    Vec3 gateSt = origin + new Vec3(colGateSt * voxelSize, rowGateSt * voxelSize, elevation);
                    Vec3 gateEd = origin + new Vec3(colGateEd * voxelSize, rowGateEd * voxelSize, elevation);
                    Vec3 gateStNear = origin + new Vec3(colGateSt * voxelSize, rowGateSt * voxelSize, elevationNear);
                    Vec3 gateEdNear = origin + new Vec3(colGateEd * voxelSize, rowGateEd * voxelSize, elevationNear);

                    //create gate for this rectangle
                    AccessibleGate gate = new AccessibleGate() { Min = gateSt, Max = gateEd, Elevation = elevation, RectanglesFrom = this };
                    this.Gates.Add(gate);
                    //create gate for nearby rectangle
                    AccessibleGate gateNearBy = new AccessibleGate() { Min = gateStNear, Max = gateEdNear, Elevation = elevationNear, RectanglesFrom = rectNear };
                    rectNear.Gates.Add(gateNearBy);
                    gate.GateTo = gateNearBy;
                    gateNearBy.GateTo = gate;
                }
            }
        }
    }
    public class AccessibleRegion
    {
        public List<Voxel> Voxels { get; set; } = new List<Voxel>();
        public List<AccessibleRectangle> Rectangles { get; set; } = new List<AccessibleRectangle>();
        public AccessibleRegion(List<Voxel> voxels)
        {
            Voxels = voxels;
            this.Voxels.ForEach(c => c.OwnerRegion = this);
        }
        public AccessibleRegion()
        {

        }
        public static List<AccessibleRegion> GenerateAccessibleRegions(List<AccessibleRectangle> rects)
        {
            List<AccessibleRegion> regions = new List<AccessibleRegion>();
          
            int accessibleRngIdx = 0;
            foreach (var rect in rects)
            {
                if (rect.Owner !=null)
                {
                    continue;
                }
                AccessibleRegion region = new AccessibleRegion();
                regions.Add(region);
                Stack<AccessibleRectangle> stkRects = new Stack<AccessibleRectangle>();
                rect.Owner = region;
                stkRects.Push(rect);
                while (stkRects.Count != 0)
                {
                    var rout = stkRects.Pop();
                    rout.Index = region.Rectangles.Count;
                    region.Rectangles.Add(rout);
                    foreach (var rAdj in rout.AdjacentRectangles)
                    {
                        if (rAdj.Owner == null)
                        {
                            rAdj.Owner = region;
                            stkRects.Push(rAdj);
                            
                        }
                    }

                }
            }
            return regions;
        }
        public AccessibleRegion(List<AccessibleRectangle> rects)
        {
            this.Rectangles = rects;
        }
        public void AppendRectangle(AccessibleRectangle rect)
        {
            this.Rectangles.Add(rect);
            rect.Owner = this;
        }
        public static List<AccessibleRegion> GenerateAccessibleRegions(VoxelDocument doc, double strideHeight, double minPassingHeight)
        {
            List<AccessibleRegion> regions = new List<AccessibleRegion>();
            Dictionary<CellIndex, List<Voxel>> cix_ObsVoxels = new Dictionary<CellIndex, List<Voxel>>();
            Dictionary<CellIndex, List<Voxel>> cix_SupVoxels = new Dictionary<CellIndex, List<Voxel>>();
            Dictionary<CellIndex, List<Voxel>> cix_TransportVoxels = new Dictionary<CellIndex, List<Voxel>>();
            //Init
            foreach (var ve in doc.Elements)
            {
                if (!ve.IsTransportElement)
                {
                    if (ve.IsActive)
                    {
                        foreach (var vox in ve.Voxels)
                        {
                            vox.IsActive = true;
                            var cix = new CellIndex(vox.ColIndex, vox.RowIndex);
                            if (ve.IsSupportElement)
                            {
                                //update ve's adj voxels
                                vox.TopAdjVoxels = new Voxel[8];//Caution: we modify the adjacent as 9
                                                                //0-east
                                                                //1-north
                                                                //2-west
                                                                //3-south
                                                                //4-northeast
                                                                //5-northwest
                                                                //6-southwest
                                                                //7-southeast

                                if (!cix_SupVoxels.ContainsKey(cix))
                                {
                                    cix_SupVoxels.Add(cix, new List<Voxel>());
                                }
                                cix_SupVoxels[cix].Add(vox);
                            }
                            else
                            {
                                if (!cix_ObsVoxels.ContainsKey(cix))
                                {
                                    cix_ObsVoxels.Add(cix, new List<Voxel>());

                                }
                                cix_ObsVoxels[cix].Add(vox);
                            }
                        }
                    }
                }
                else //update transport voxels
                {
                    foreach (var vox in ve.Voxels)
                    {
                        var cix = new CellIndex(vox.ColIndex, vox.RowIndex);
                        vox.TopAdjVoxels = null;
                        //0-linked support voxels,which will be updated later
                        if (vox.BottomVoxel != null)
                            vox.LinkedTransportVoxels.Add(vox.BottomVoxel);
                        if (vox.TopVoxel != null)
                            vox.LinkedTransportVoxels.Add(vox.TopVoxel);

                        if (cix_TransportVoxels.ContainsKey(cix))
                        {
                            cix_TransportVoxels[cix].Add(vox);
                        }
                        else
                        {
                            cix_TransportVoxels.Add(cix, new List<Voxel>() { vox });
                        }
                    }
                }
            }
            //Merge intersected voxel
            var keyColl = cix_ObsVoxels.Keys.ToList();
            foreach (var key in keyColl)
            {
                var voxelOriginal = cix_ObsVoxels[key];
                var mergedVoxels = MergeIntersectingVoxels(voxelOriginal);
                cix_ObsVoxels[key] = mergedVoxels;
            }
            //merge support voxels
            keyColl = cix_SupVoxels.Keys.ToList();
            foreach (var key in keyColl)
            {
                var voxOriginal = cix_SupVoxels[key];
                var mergedVoxels = MergeIntersectingVoxels(voxOriginal);
                cix_SupVoxels[key] = mergedVoxels;
            }
            //merge transport voxels
            keyColl = cix_TransportVoxels.Keys.ToList();
            foreach (var key in keyColl)
            {
                var voxOriginal = cix_TransportVoxels[key];
                var mergetVoxels = MergeIntersectingVoxels(voxOriginal);
                cix_TransportVoxels[key] = mergetVoxels;
            }
            //support voxel Adjacency test(8-adjacent)
            CellIndex[] adjCellIndex = new CellIndex[8] { new CellIndex(1, 0), new CellIndex(0, 1), new CellIndex(-1, 0), new CellIndex(0, -1), new CellIndex(1, 1), new CellIndex(-1, 1), new CellIndex(-1, -1), new CellIndex(1, -1) };
            foreach (var supVoxels in cix_SupVoxels.Values)
            {
                foreach (var voxSup in supVoxels)
                {
                    CellIndex voxIndex = new CellIndex(voxSup.ColIndex, voxSup.RowIndex);
                    //search vox-adj rel
                    for (int j = 0; j <= 7; j++)
                    {
                        var cellIdxNear = voxIndex + adjCellIndex[j];
                        voxSup.TopAdjVoxels[j] = null;
                        if (cix_SupVoxels.ContainsKey(cellIdxNear))
                        {
                            var potentialVoxAdj = cix_SupVoxels[cellIdxNear];
                            foreach (var voxAdj in potentialVoxAdj)
                            {
                                double voxAdjTop = voxAdj.TopElevation;
                                double voxAdjBottom = voxAdj.BottomElevation;
                                if (Math.Abs(voxAdj.TopElevation - voxSup.TopElevation) <= strideHeight)
                                {
                                    voxSup.TopAdjVoxels[j] = voxAdj;
                                }
                                if (voxAdjBottom > voxSup.TopElevation)
                                {
                                    break;
                                }
                            }
                        }
                    }
                    //search potential transport rel
                    if (cix_TransportVoxels.ContainsKey(voxIndex))
                    {
                        var transVoxes = cix_TransportVoxels[voxIndex];
                        //find transport voxels intersecting with the support voxels
                        var transVox = transVoxes.Where(c => voxSup.TopElevation + strideHeight > c.BottomElevation && voxSup.TopElevation < c.TopElevation).FirstOrDefault();
                        if (transVox != null)
                        {
                            //link vox to transVox
                            voxSup.LinkedTransportVoxels.Add(transVox);
                            //link transvox to vox
                            transVox.LinkedSupportVoxels.Add(voxSup);
                        }
                    }
                }
            }
            //transport voxel adjacent text
            foreach (var transVoxels in cix_TransportVoxels.Values)
            {
                foreach (var transVox0 in transVoxels)
                {
                    foreach (var transVox1 in transVoxels)
                    {
                        if (transVox0 != transVox1)
                        {
                            transVox0.LinkedTransportVoxels.Add(transVox1);
                        }
                    }
                }
            }
            //voxel navigability test
            foreach (var supVoxes in cix_SupVoxels.Values)
            {
                //check if any supprot voxels obstruct voxel
                foreach (var vox in supVoxes)
                {
                    var cix = new CellIndex(vox.ColIndex, vox.RowIndex);
                    var voxTop = vox.TopElevation;
                    //check if current voxel is navigable
                    //get voxels
                    var voxAbove = vox.TopVoxel;
                    var scanElev0 = vox.TopElevation + strideHeight;
                    var scanElev1 = vox.TopElevation + minPassingHeight;
                    //check if there are support voxels cover or obstruct current voxels
                    if (voxAbove != null && voxAbove.BottomElevation <= scanElev1)
                    {
                        vox.Navigable = false;
                        //break;
                    }
                    //check if any obstruction voxels obstructs voxel
                    if (cix_ObsVoxels.ContainsKey(cix))
                    {
                        var potentialObsVoxes = cix_ObsVoxels[cix];
                        var obsElev0 = voxTop + strideHeight;
                        var obsElev1 = voxTop + minPassingHeight;
                        foreach (var voxOb in potentialObsVoxes)
                        {
                            var voxObTop = voxOb.TopElevation;
                            var voxObBtm = voxOb.BottomElevation;
                            if (voxObBtm <= obsElev1 && voxObTop >= obsElev0) //Obstruction detected
                            {
                                vox.Navigable = false;
                                break;
                            }
                            else if (voxObBtm > obsElev1) //ob out of scan range
                            {
                                break;
                            }
                        }
                    }
                }
            }
            //generate accessible region
            HashSet<Voxel> voxScanned = new HashSet<Voxel>();
            foreach (var voxes in cix_SupVoxels.Values)
            {
                foreach (var vox in voxes)
                {
                    if (!voxScanned.Contains(vox) && vox.Navigable)
                    {
                        List<Voxel> voxAccessibleRng = new List<Voxel>();
                        Stack<Voxel> vox2Check = new Stack<Voxel>();
                        vox2Check.Push(vox);
                        voxScanned.Add(vox);
                        int arIndex = regions.Count;
                        while (vox2Check.Count != 0)
                        {
                            var voxOut = vox2Check.Pop();
                            voxAccessibleRng.Add(voxOut);
                            //add the adjacent voxels, linked tranport and support voxels(tranport voxels only)
                            var voxesNear = new List<Voxel>();
                            if (voxOut.TopAdjVoxels != null)
                            {
                                voxesNear.AddRange(voxOut.TopAdjVoxels.Where(c => c != null));
                            }
                            voxesNear.AddRange(voxOut.LinkedSupportVoxels);
                            voxesNear.AddRange(voxOut.LinkedTransportVoxels);
                            if (voxesNear.Count != 0)
                            {
                                //support to support navigation
                                foreach (var voxNear in voxesNear)
                                {
                                    if (voxNear != null && voxNear.Navigable && !(voxScanned.Contains(voxNear)))
                                    {
                                        voxScanned.Add(voxNear);
                                        vox2Check.Push(voxNear);
                                    }
                                }
                            }
                            //transport to support navigation
                        }
                        AccessibleRegion ar = new AccessibleRegion(voxAccessibleRng);
                        regions.Add(ar);
                    }
                }
            }
            return regions;
        }

        public List<AccessibleCell> GenerateAccessibleCells()
        {
            return null;
            foreach (var rect in this.Rectangles)
            {
                 
            }
        }
        private static List<Voxel> MergeIntersectingVoxels(List<Voxel> voxWithSameIndex)
        {
            var sortedVoxels = voxWithSameIndex.OrderBy(c => c.BottomElevation).ToList();
            int snakePointer = 0;
            int foodPointer = 1;
            List<Voxel> mergedVoxels = new List<Voxel>();
            var snakeVoxel = sortedVoxels[snakePointer];
            snakeVoxel.Parent = snakeVoxel;
            mergedVoxels.Add(snakeVoxel);
            while (foodPointer < voxWithSameIndex.Count)
            {
                Voxel foodVoxel = sortedVoxels[foodPointer];
                //check if snake voxels intersects food voxels
                if (Math.Round(snakeVoxel.BottomElevation - foodVoxel.TopElevation, 4) <= 0 &&
                    Math.Round(snakeVoxel.TopElevation - foodVoxel.BottomElevation, 4) >= 0)
                {
                    snakeVoxel.TopElevation = Math.Max(snakeVoxel.TopElevation, foodVoxel.TopElevation);
                    foodVoxel.Parent = snakeVoxel;
                    foodPointer += 1;
                }
                else //snake voxel and food voxel do not intersect
                {
                    snakePointer = foodPointer;
                    snakeVoxel = sortedVoxels[snakePointer];
                    snakeVoxel.Parent = snakeVoxel;
                    mergedVoxels.Add(snakeVoxel);
                    foodPointer += 1;
                }
            }
            //link voxels in mergrdVoxels
            for (int i = 0; i < mergedVoxels.Count - 1; i++)
            {
                var vox = mergedVoxels[i];
                var voxAbove = mergedVoxels[i + 1];
                vox.TopVoxel = voxAbove;
                voxAbove.BottomVoxel = vox;
            }
            return mergedVoxels;
        }
       
        

       
        


       

       
        
    }
    public class AccessibleCell
    {
        public AccessibleRectangle Owner { get; set; }
        public CellIndex Index { get; set; }
        public AccessibleCell[] Neighbours { get; set; }
        public double G { get; set; }=double.MaxValue;
        public double H { get; set; } = double.MaxValue;
        public double F { get; set; } = double.MaxValue;
        public AccessibleCell Previous { get; set; }

        public double Get_Elevation()
        {
            return Owner.Elevation;
        }
        
    }

    public class AccessibleGate : IAStarObject
    {
        public Vec3 LocationPoints { get; set; } = null;
        public WayPoint[] WayPoints { get; set; }
        public Vec3 Min { get; set; }
        public Vec3 Max { get; set; }
        public double Elevation { get; set; }
        public double G { get; set; } = double.MaxValue;
        public double H { get; set; } = double.MaxValue;
        public double F { get; set; } = double.MaxValue;
        public bool IsOpen { get; set; } = false;
        public bool IsClose { get; set; } = false;
        public AccessibleRectangle RectanglesFrom { get; set; }

        public AccessibleGate GateTo{ get; set; }
        public AccessibleGate Previous { get; internal set; }
        public double DistanceTo(AccessibleGate other)
        {
            Vec3 pt0 = WayPoints[0].Points;
            Vec3 pt1 = WayPoints[WayPoints.Length - 1].Points;
            Vec3 pta = other.WayPoints[0].Points;
            Vec3 ptb = other.WayPoints[other.WayPoints.Length - 1].Points;
            double distance = double.MaxValue;
            if (pt0.X == pt1.X && pta.X == ptb.X)
            {
                if (pt0.Y < ptb.Y && pt1.Y > pta.Y) //intersection
                {
                    return Math.Abs(pta.X - pt0.X);
                }
                else
                {
                    double dblDistance = double.MaxValue;
                    var ptsBase = new Vec3[2] { pt0, pt1 };
                    var ptOther = new Vec3[2] { pta, ptb };
                    foreach (var p0 in ptsBase)
                    {
                        foreach (var p1 in ptOther)
                        {
                            double dblDisTemp = (p0 - p1).GetLength();
                            if (dblDistance > dblDisTemp)
                                dblDistance = dblDisTemp;
                        }
                    }
                    return dblDistance;
                }
            }
            else if (pt0.Y == pt1.Y && pta.Y == ptb.Y)
            {
                if (pt0.X < ptb.X && pt1.X > pta.X) //intersection
                {
                    return Math.Abs(pta.Y - pt0.Y);
                }
                else
                {
                    double dblDistance = double.MaxValue;
                    var ptsBase = new Vec3[2] { pt0, pt1 };
                    var ptOther = new Vec3[2] { pta, ptb };
                    foreach (var p0 in ptsBase)
                    {
                        foreach (var p1 in ptOther)
                        {
                            double dblDisTemp = (p0 - p1).GetLength();
                            if (dblDistance > dblDisTemp)
                                dblDistance = dblDisTemp;
                        }
                    }
                    return dblDistance;
                }
            }
            else
            {
                double dblDistance = double.MaxValue;
                var ptsBase = new Vec3[2] { pt0, pt1 };
                var ptOther = new Vec3[2] { pta, ptb };
                foreach (var p0 in ptsBase)
                {
                    foreach (var p1 in ptOther)
                    {
                        double dblDisTemp = (p0 - p1).GetLength();
                        if (dblDistance > dblDisTemp)
                            dblDistance = dblDisTemp;
                    }
                }
                return dblDistance;
            }

        }

        public double DistanceTo(Vec3 pt, out Vec3 ptOnGateNear)
        {
            Vec3 pt0 = Min;
            Vec3 pt1 = Max;
            if (pt0.X == pt1.X)//vertical gate
            {
                if (pt.Y > pt0.Y && pt.Y < pt1.Y) //pt falls in between the gate
                {
                    ptOnGateNear = new Vec3(pt0.X, pt.Y, pt0.Z);
                    return Math.Abs(pt.X - pt0.X);
                }
                else// pt is onut of the gate
                {
                    ptOnGateNear = null;
                    var disPt20 = (pt - pt0).GetLength();
                    var disPt21 = (pt - pt1).GetLength();
                    if (disPt20 < disPt21)
                    {
                        ptOnGateNear = pt0;
                        return disPt20;
                    }
                    else
                    {
                        ptOnGateNear = pt1;
                        return disPt21;
                    }
                }
            }
            else if (pt0.Y == pt1.Y) //horizontal gate
            {
                if (pt.X > pt0.X && pt.X < pt1.X)// pt falls in between ptx
                {
                    ptOnGateNear = new Vec3(pt.X, pt0.Y, pt0.Z);
                    return Math.Abs(pt.Y - pt0.Y);
                }
                else //pt is out of the gate
                {
                    ptOnGateNear = null;
                    var disPt20 = (pt - pt0).GetLength();
                    var disPt21 = (pt - pt1).GetLength();
                    if (disPt20 < disPt21)
                    {
                        ptOnGateNear = pt0;
                        return disPt20;
                    }
                    else
                    {
                        ptOnGateNear = pt1;
                        return disPt21;
                    }
                }
            }
            else
            {
                ptOnGateNear = null;
                var disPt20 = (pt - pt0).GetLength();
                var disPt21 = (pt - pt1).GetLength();
                if (disPt20 < disPt21)
                {
                    ptOnGateNear = pt0;
                    return disPt20;
                }
                else
                {
                    ptOnGateNear = pt1;
                    return disPt21;
                }
            }
        }

        public void GenerateWayPoints()
        {
            List<WayPoint> wps = new List<WayPoint>();
            wps.Add(new WayPoint(this.Min));
            if ((this.Max - this.Min).GetSquareLen() > 1e-4) // the max and min are not the same
            {
                wps.Add(new WayPoint(this.Max));

            }
            wps.Add(new WayPoint(this.LocationPoints));
            this.WayPoints = wps.ToArray();
        }
    }
    public class WayPoint : IAStarObject
    {
        public Vec3 Points { get; set; }
        public WayPoint Previous { get; set; }
        public List<AccessibleRectangle> Rectangles { get; set; } = new List<AccessibleRectangle>();

        public double G { get; set; } = double.MaxValue;
        public double H { get; set; } = double.MaxValue;
        public double F { get; set; } = double.MaxValue;
        public bool IsOpen { get; set; } = false;
        public bool IsClose { get; set; } = false;

        public AccessibleGate Gate { get; set; }

        public WayPoint(Vec3 point)
        {
            this.Points = point;
        }
        public WayPoint()
        {

        }
        public List<WayPoint> AdjacentWayPoints { get; set; } = new List<WayPoint>();
    }
    public interface IAStarObject
    {
        double G { get; set; }
        double H { get; set; }
        double F { get; set; }

        bool IsOpen { get; set; }
        bool IsClose { get; set; }
    }
    #endregion
    #region Model Converter
    public class MeshDocumentConverter
    {
        public void SaveMeshDocument(string path,MeshDocument doc)
        {
            try
            {
                //3 files are saved
                //file 1:vertices,triangles(bin)
                //file 2:transforms(bin)
                //file 3 Document
                //                 Titile
                //                 Transform
                //                 MeshElement
                //                      element Id
                //                      IsSymbol
                //                      solids
                //                           verticesIndex
                //                           triangleIndex
                //                 MeshInstacnes
                //                      SymoblIndexes
                //                      TransformIndexes
               
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace);
            }
        }
        public void LoadMeshDocument(string path)
        {
            try
            {
                
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace);
            }
        }
    }
    //save voxel as obj formats
    public class VoxelObjConverter
    {

    }
    #endregion

    #region voxel tool
    public static class LEGOVoxelTool
    {
        
        public static CompressedVoxelDocument CompressVoxelDocuments (ref Dictionary<CellIndex3D,int> scales, VoxelDocument voxDoc)
        {
            CompressedVoxelDocument cvd = new CompressedVoxelDocument(); 
            cvd. VoxelSize = voxDoc.VoxelSize;
            cvd.  Origin = voxDoc.Origin;
            Dictionary<int, int> voxHeigtMM_VoxelIndex = new Dictionary<int, int>();
            cvd.Elements = new List<CompressedVoxelElement>();
            foreach (var ve in voxDoc.Elements)
            {
                var rects = CompressVoxels(ref scales, ve);
                var compElem = new CompressedVoxelElement(ve, rects);
                cvd. Elements.Add(compElem);
            }
            cvd.VoxelHight = voxHeigtMM_VoxelIndex;
            return cvd;
        }
        public static List<VoxelRectangle> CompressVoxels(ref Dictionary<CellIndex3D,int> scales, VoxelElement ve)
        {
            Dictionary<int, Dictionary<CellIndex, int>> dicBtmElev_Cix_Height = new Dictionary<int, Dictionary<CellIndex, int>>();
            //get or update voxelHeightIndex
            
            foreach (var vox in ve.Voxels)
            {
                int dblHeight_MM = (int)Math.Round((vox.TopElevation - vox.BottomElevation)*304.8);
                //update dicBtmElev_Cix_HeightIdx
                var vbtm_MM =(int) Math.Round (vox.BottomElevation*304.8);
                var voxCellIdx = new CellIndex(vox.ColIndex, vox.RowIndex);
                if (!dicBtmElev_Cix_Height.ContainsKey(vbtm_MM))
                {
                    dicBtmElev_Cix_Height.Add(vbtm_MM, new Dictionary<CellIndex, int>()
                    {
                        { voxCellIdx,dblHeight_MM  }
                    });
                }
                else if (!dicBtmElev_Cix_Height[vbtm_MM].ContainsKey (voxCellIdx))
                {
                    dicBtmElev_Cix_Height[vbtm_MM].Add (voxCellIdx, dblHeight_MM);
                }
            }
            //Generate voxelRactangle
            List<VoxelRectangle> voxelRectangle = new List<VoxelRectangle>();
            foreach (var btmElev_cix_Height in dicBtmElev_Cix_Height)
            {
                var btmElev_MM = btmElev_cix_Height.Key;
                var cix_Height_MM = btmElev_cix_Height.Value;
                HashSet<CellIndex> cixChecked = new HashSet<CellIndex>();
                
                foreach (var cix in cix_Height_MM.Keys)
                {
                    if(!cixChecked.Contains (cix))
                    {
                        var vr = GenerateRectangleAndMarkVoxels(cix_Height_MM,cixChecked, ref scales, btmElev_MM, cix, voxelRectangle.Count, out var scale);
                        voxelRectangle.Add(vr);
                        //update cixChecked
                        foreach (var vix in vr.Get_CellIndexes(scale))
                        {
                            cixChecked.Add(vix);
                        }
                    }
                }
            }
            return voxelRectangle;
        }

        private static VoxelRectangle GenerateRectangleAndMarkVoxels(Dictionary<CellIndex, int> voxelOriginal, HashSet<CellIndex> cixChecked,ref Dictionary<CellIndex3D,int> scales, double bottomElev, CellIndex voxStart, int rectIndex, out CellIndex3D scale)
        {
            var voxMax = voxStart;
            var voxMin = voxStart;
            int voxelIndex = voxelOriginal[voxStart];
            CellIndex voxMinRight = voxStart;
            CellIndex voxMaxLeft = voxStart;
            bool minFound = false;
            bool maxFound = false;
            while (!minFound || !maxFound)
            {
                List<CellIndex> voxE = null;
                List<CellIndex> voxS = null;
                List<CellIndex> voxW = null;
                List<CellIndex> voxN = null;
                CellIndex voxNE = null;
                CellIndex voxSW = null;
                CellIndex voxSE = null;
                CellIndex voxNW = null;
                if (!maxFound) //voxMax has not been found yet
                {
                    voxNE = GetDirectionVoxel(voxelOriginal,cixChecked, voxelIndex, voxMax, voxMin, voxMaxLeft, voxMinRight, "NE");
                    voxN = GetDirectionVoxels(voxelOriginal,cixChecked, voxelIndex, voxMax, voxMin, "N");
                    voxE = GetDirectionVoxels(voxelOriginal,cixChecked, voxelIndex, voxMax, voxMin, "E");
                }
                if (!minFound) //voxMin has not been found yet
                {
                    voxSW = GetDirectionVoxel(voxelOriginal,cixChecked, voxelIndex, voxMax, voxMin, voxMaxLeft, voxMinRight, "SW");
                    voxSE = GetDirectionVoxel(voxelOriginal,cixChecked, voxelIndex, voxMax, voxMin, voxMaxLeft, voxMinRight, "SE");
                    voxS = GetDirectionVoxels(voxelOriginal,cixChecked, voxelIndex, voxMax, voxMin, "S");
                    voxNW = GetDirectionVoxel(voxelOriginal,cixChecked, voxelIndex, voxMax, voxMin, voxMaxLeft, voxMinRight, "NW");
                    voxW = GetDirectionVoxels(voxelOriginal,cixChecked, voxelIndex, voxMax, voxMin, "W");
                }
                
                //mark found voxels
                if (voxE != null && voxNE != null && voxN != null) //max=max.NE
                {
                    voxMax = voxNE;
                    if (voxS != null && voxW != null && voxSE != null && voxNW != null && voxSW != null) //min =min.SW
                    {
                        voxMin = voxSW;
                        voxMinRight = voxSE;
                        voxMaxLeft = voxNW;
                    }
                    else if (voxS != null && voxSE != null)
                    {
                        voxMin = voxMin + new CellIndex(0, -1);// voxMin.TopAdjVoxels[3];//S
                        voxMinRight = voxSE;
                        voxMaxLeft = voxN.Last();
                    }
                    else if (voxW != null && voxNW != null)
                    {
                        voxMin += new CellIndex(-1, 0);// voxMin.TopAdjVoxels[2];//W
                        voxMinRight = voxE.Last(); ;
                        voxMaxLeft = voxNW;
                    }
                    else
                    {
                        minFound = true;
                    }
                }
                /*
                 2 max(t+1)=max(t) +(0,1)
                        N 
                        2.1  min(t+1)=nw
                            S,W,SW,NW
                        2.2  min(t+1)=min(t)-(0,1)
                            S
                        2.3  min(t+1)=min(t)-(1,0)
                            W，NW
                        2.4  min(t+1)=min(t)
                            Else	 
                 */
                else if (voxN != null) //voxMax=voxN
                {
                    voxMax += new CellIndex(0, 1);//voxMax.TopAdjVoxels[1];
                   
                    //2.1  min(t+1)=nw
                    //S,W,SW,NW
                    if (voxS != null && voxW != null && voxSW != null && voxNW != null)
                    {
                        voxMin += new CellIndex(-1, -1); // = voxMin.TopAdjVoxels[6];
                        voxMinRight = voxS.Last(); ;
                        voxMaxLeft = voxNW;
                    }
                    //2.2  min(t+1)=min(t)-(0,1)
                    //S
                    else if (voxS != null)
                    {
                        voxMinRight = voxS.Last(); ;
                        voxMaxLeft = voxN.Last();
                        voxMin += new CellIndex(0, -1); //voxMin.TopAdjVoxels[3];
                    }
                    //2.3  min(t+1)=min(t)-(1,0)
                    //W，NW
                    else if (voxW != null && voxNW != null) //voxMin
                    {
                        voxMin += new CellIndex(-1, 0); //= voxMin.TopAdjVoxels[2];
                        voxMaxLeft = voxNW;
                    }
                    //2.4  min(t + 1) = min(t)
                    //Else
                    else
                    {
                        minFound = true;
                    }
                }
                /*
                 3 max(t+1)=max(t) +(1,0)
                    E 
                    3.1  min(t+1)=SW
                        S,W,SW,SE
                    3.2  min(t+1)=min(t)-(0,1)
                        S，SE
                    3.3  min(t+1)=min(t)-(1,0)
                        W
                    3.4  min(t+1)=min(t)
                        Else	
                 */

                else if (voxE != null)
                {
                    voxMax += new CellIndex(1, 0); //= voxMax.TopAdjVoxels[0];
                    if (voxS != null && voxW != null && voxSW != null && voxSE != null)
                    {
                        voxMin += new CellIndex(-1, -1);   //voxMin.TopAdjVoxels[6];
                        voxMinRight = voxSE; ;
                        voxMaxLeft = voxW.Last();
                    }
                    else if (voxS != null && voxSE != null)
                    {
                        voxMin += new CellIndex(0, -1);// = voxMin.TopAdjVoxels[3];
                        voxMinRight = voxSE;
                    }
                    else if (voxW != null)
                    {
                        voxMinRight = voxE.Last();
                        voxMin += new CellIndex(-1, 0); //voxMin.TopAdjVoxels[2];
                    }
                    else
                    {
                        minFound = true;
                    }
                }
                else
                {
                    maxFound = true;
                    if (voxW != null && voxS != null && voxSW != null)
                    {
                        voxMin +=new CellIndex(-1, -1); //= voxMin.TopAdjVoxels[6];
                        voxMinRight = voxS.Last();
                        voxMaxLeft = voxW.Last();
                    }
                    else if (voxS != null)
                    {
                        voxMin += new CellIndex(0, -1);// = voxMin.TopAdjVoxels[3];
                        voxMinRight = voxS.Last();
                    }
                    else if (voxW != null)
                    {
                        voxMin += new CellIndex(-1, 0);//= voxMin.TopAdjVoxels[2];
                        voxMaxLeft = voxW.Last();
                    }
                    else
                    {
                        minFound = true;
                    }
                }
            }
            //collect boundary voxels
            
            //Genearte accessible region
            VoxelRectangle rect = new VoxelRectangle();
            rect.Start = new CellIndex(voxMin.Col, voxMin.Row);
            var scale2D = voxMax - voxMin;
            scale =new CellIndex3D (scale2D.Col ,scale2D.Row ,voxelIndex);
            int scaleIdx = -1;
            if(scales.ContainsKey (scale))
            {
                scaleIdx = scales [scale];
            }
            else
            {
                scaleIdx = scales.Count;
                scales.Add(scale,scaleIdx);
            }
            rect.ScaleIndex = scaleIdx;
            rect.BottomElevation = (int)bottomElev;
            return rect;
        }
        /// <summary>
        /// Get voxels at a direction of a rectangle zone defined by voxMax and voxMin
        /// </summary>
        /// <param name="voxMax">the upper right voxels</param>
        /// <param name="voxMin">the bottom left voxels</param>
        /// <param name="strDir">string,SW,NW,SE,NE</param>
        /// <returns>voxels, null if one or more voxels in current direction is invalid</returns>
        private static CellIndex GetDirectionVoxel(Dictionary<CellIndex,int> voxelOriginal,HashSet<CellIndex> indexChecked, int voxelTypeIndex,  CellIndex voxMax, CellIndex voxMin, CellIndex voxMaxLeft, CellIndex voxMinRight, string strDir)
        {
            CellIndex voxCur = null;
            switch (strDir)
            {
                case "NE":
                    voxCur = voxMax+new CellIndex (1,1);
                    if (!voxelOriginal.ContainsKey(voxCur) || voxelOriginal[voxCur] != voxelTypeIndex || indexChecked.Contains(voxCur)) 
                        return null;
                    break;
                case "NW":
                    voxCur = voxMaxLeft+new CellIndex (-1,1);
                    if (!voxelOriginal.ContainsKey(voxCur) || voxelOriginal[voxCur] != voxelTypeIndex || indexChecked.Contains(voxCur))
                        return null;
                    break;
                case "SE":
                    voxCur = voxMinRight+new CellIndex(1,-1);
                    if (!voxelOriginal.ContainsKey(voxCur) || voxelOriginal[voxCur] != voxelTypeIndex || indexChecked.Contains(voxCur))
                        return null;
                    break;

                case "SW":
                    voxCur = voxMin+new CellIndex(-1, -1); ;
                    if (!voxelOriginal.ContainsKey(voxCur) || voxelOriginal[voxCur] != voxelTypeIndex || indexChecked.Contains(voxCur))
                        return null;
                    break;
            }
            return voxCur;
        }

        /// <summary>
        /// Get voxels at a direction of a rectangle zone defined by voxMax and voxMin
        /// </summary>
        /// <param name="voxMax">the upper right voxels</param>
        /// <param name="voxMin">the bottom left voxels</param>
        /// <param name="strDir">string,E-ease,W-west,N-north,S-south</param>
        /// <returns>voxels, null if one or more voxels in current direction is invalid</returns>
        private static List<CellIndex> GetDirectionVoxels(Dictionary<CellIndex, int> voxelOriginal, HashSet<CellIndex> indexChecked,   int voxelTypeIndex, CellIndex voxMax, CellIndex voxMin, string strDir)
        {
            int colSt = voxMin.Col;
            int colEd = voxMax.Col;
            int rowSt = voxMin.Row;
            int rowEd = voxMax.Row;
            List<CellIndex> result = new List<CellIndex>();
            switch (strDir)
            {
                case "E":
                    CellIndex voxCur = voxMax+new CellIndex (1,0);
                    if (!voxelOriginal.ContainsKey(voxCur) || voxelOriginal[voxCur] != voxelTypeIndex || indexChecked.Contains(voxCur))
                        return null;
                    result.Add(voxCur);
                    for (int row = rowEd - 1; row >= rowSt; row--) //search backward
                    {
                        voxCur = voxCur+new CellIndex (0,-1);
                        if (!voxelOriginal.ContainsKey(voxCur) || voxelOriginal[voxCur] != voxelTypeIndex || indexChecked.Contains(voxCur))
                            return null;
                        result.Add(voxCur);
                    }
                    break;
                case "W":
                    voxCur = voxMin+new CellIndex (-1,0);
                    if (!voxelOriginal.ContainsKey(voxCur) || voxelOriginal[voxCur] != voxelTypeIndex || indexChecked.Contains(voxCur))
                            return null;
                    
                    result.Add(voxCur);
                    for (int row = rowSt + 1; row <= rowEd; row++)
                    {
                        voxCur = voxCur+new CellIndex (0,1);
                        if (!voxelOriginal.ContainsKey(voxCur) || voxelOriginal[voxCur] != voxelTypeIndex || indexChecked.Contains(voxCur))
                            return null;
                        result.Add(voxCur);
                    }
                    break;
                case "S":
                    voxCur = voxMin+new CellIndex (0,-1);
                    if (!voxelOriginal.ContainsKey(voxCur) || voxelOriginal[voxCur] != voxelTypeIndex || indexChecked.Contains(voxCur))
                        return null;
                    result.Add(voxCur);
                    for (int col = colSt + 1; col <= colEd; col++)
                    {
                        voxCur = voxCur+new CellIndex (1,0);
                        if (!voxelOriginal.ContainsKey(voxCur) || voxelOriginal[voxCur] != voxelTypeIndex || indexChecked.Contains(voxCur))
                            return null;
                        result.Add(voxCur);
                    }
                    break;
                case "N":
                    voxCur = voxMax+new CellIndex (0,1);
                    if (!voxelOriginal.ContainsKey(voxCur) || voxelOriginal[voxCur] != voxelTypeIndex || indexChecked.Contains(voxCur))
                        return null;
                    result.Add(voxCur);
                    for (int col = colEd - 1; col >= colSt; col--)
                    {
                        voxCur = voxCur+new CellIndex(-1,0);
                        if (!voxelOriginal.ContainsKey(voxCur) || voxelOriginal[voxCur] != voxelTypeIndex || indexChecked.Contains(voxCur))
                            return null;
                        result.Add(voxCur);
                    }
                    break;
            }
            return result;
        }
        /// <summary>
        /// Save compressed voxels
        /// </summary>
        /// <param name="compVoxDoc">the compressed voxels</param>
        /// <param name="filePath">file path</param>
        public static void SaveCompressedVoxelDocument(CompressedVoxelDocument compVoxDoc,  string filePath)
        {
            FileStream fs = new FileStream(filePath,FileMode.Create);
            BinaryWriter bw = new BinaryWriter(fs);
            try
            {
                //write voxel info
                bw.Write(Vec32Byte(compVoxDoc.Origin));//origin
                bw.Write(BitConverter.GetBytes(compVoxDoc.VoxelSize));//voxel size;
                bw.Write(BitConverter.GetBytes(compVoxDoc.Elements.Count));//element count                                                   //write elem
                foreach (var elem in compVoxDoc.Elements)
                {
                    bw.Write(CompressedVoxelElems2Bytes(elem));
                }
                bw.Flush();
                
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace);
            }
            finally
            {
                bw.Close();
                fs.Close();
            }

        }

        public static CompressedVoxelDocument LoadCompressedVoxelDocument(string filePath)
        {
            BinaryReader br = new BinaryReader(new FileStream(filePath, FileMode.Open));
            try
            {
                //load origin
                double x = br.ReadDouble();
                double y = br.ReadDouble();
                double z=br.ReadDouble();
                var origin = new Vec3(x, y, z);
                //lode voxelsze
                double dblVoxSize = br.ReadDouble();
                //load scales
                int scaleCount = br.ReadInt32();
                List<CellIndex3D> scales = new List<CellIndex3D>();
                for(int i=0;i<scaleCount;i++)
                {
                    scales.Add(new CellIndex3D(br.ReadInt32(), br.ReadInt32(), br.ReadInt32()));
                }
                //load element
                int numElem=br.ReadInt32();
                List<CompressedVoxelElement> elems = new List<CompressedVoxelElement>() { Capacity = numElem };
                for(int i=0;i<=numElem -1;i++)
                {
                    //Id
                    byte strLen = br.ReadByte();
                    var stringByte = new byte[strLen];
                    for (int j = 0; j < strLen; j++)
                    {
                        stringByte[j] = br.ReadByte();
                    }
                    var elemId = Encoding.Default.GetString(stringByte);//load string
                    //Name
                    strLen = br.ReadByte();
                    stringByte = new byte[strLen];
                    for (int j = 0; j < strLen; j++)
                    {
                        stringByte[j] = br.ReadByte();
                    }
                    var elemName = Encoding.Default.GetString(stringByte);//load string

                    //Category
                    strLen = br.ReadByte();
                    stringByte = new byte[strLen];
                    for (int j = 0; j < strLen; j++)
                    {
                        stringByte[j] = br.ReadByte();
                    }
                    var elemCat = Encoding.Default.GetString(stringByte);//load string
                    /*
                    bool isSupport = cve.IsSupportElement;
                    bool isObstruct = cve.IsObstructElement;
                    bool isTransport = cve.IsTransportElement;
                    bool isActive = cve.IsActive;
                    */
                    bool isSupport=br.ReadBoolean();
                    bool isObstruct=br.ReadBoolean();
                    bool isTransport = br.ReadBoolean();
                    bool isActive = br.ReadBoolean();
                    //rectangle
                    /*
                    var rSt = rect.Start;
                    var rScale = rect.Scale;
                    var rBtmElev = rect.BottomElevation;
                    List<byte> result = new List<byte>();
                    result.AddRange(CellIndex2Byte(rSt));
                    result.AddRange(CellIndex3D2Byte(rScale));
                    result.AddRange(BitConverter.GetBytes(rBtmElev));
                    */
                    int numRects = br.ReadInt32();
                    List<VoxelRectangle> rects = new List<VoxelRectangle>();
                    for(int j=0;j<=numRects-1;j++)
                    {
                        var rSt = new CellIndex(br.ReadInt32(), br.ReadInt32());
                        var rScaleIdx =br.ReadInt32();
                        var rBtmElev = br.ReadInt32();
                        VoxelRectangle vRect = new VoxelRectangle() { BottomElevation = rBtmElev, Start = rSt, ScaleIndex= rScaleIdx };
                        rects.Add(vRect);
                    }
                    CompressedVoxelElement cve = new CompressedVoxelElement() { ElementId=elemId,Category=elemCat,Name =elemName,IsActive=isActive,IsObstructElement=isObstruct,IsTransportElement =isTransport,IsSupportElement =isSupport,VoxelRectangles=rects};
                    elems.Add (cve);
                }
                CompressedVoxelDocument result = new CompressedVoxelDocument();
                result.Origin = origin;
                result.VoxelSize = dblVoxSize;
                result.Elements = elems;
                result.VoxelScale = scales;
                return result;
                
            }
            finally
            {
                br.Close();
            }
        }
        public static byte[] CompressedVoxelElems2Bytes(CompressedVoxelElement cve)
        {
            string cId = cve.ElementId;
            string cNa = cve.Name;
            string cCat = cve.Category;
            bool isSupport = cve.IsSupportElement;
            bool isObstruct = cve.IsObstructElement;
            bool isTransport = cve.IsTransportElement;
            bool isActive = cve.IsActive;
            List<byte> result = new List<byte>();
            result.AddRange(String2Bytes(cId));
            result.AddRange(String2Bytes(cNa));
            result.AddRange(String2Bytes(cCat));
            result.AddRange(BitConverter.GetBytes(isSupport));
            result.AddRange(BitConverter.GetBytes(isObstruct));
            result.AddRange(BitConverter.GetBytes(isTransport));
            result.AddRange(BitConverter.GetBytes(isActive));
            //number of rects
            result.AddRange(BitConverter.GetBytes(cve.VoxelRectangles.Count));
            foreach (var rect in cve.VoxelRectangles)
            {
                result.AddRange(Rect2Bytes(rect));
            }
            return result.ToArray();
        }
        public static byte[] String2Bytes(string str)
        {
            List<byte> result=new List<byte>();
            var stringByte = Encoding.Default.GetBytes(str);
            result.Add((byte)stringByte.Length);
            result.AddRange(stringByte);
            return result.ToArray();
        }
        public static byte[] Vec32Byte(Vec3 input)
        {
            List<byte> results = new List<byte>();
            results.AddRange(BitConverter.GetBytes(input.X));
            results.AddRange(BitConverter.GetBytes(input.Y));
            results.AddRange(BitConverter.GetBytes(input.Z));
            return results.ToArray();
        }
       
        public static byte[] Rect2Bytes(VoxelRectangle rect)
        {
            var rSt = rect.Start;
            var rScaleIdx = rect.ScaleIndex;
            var rBtmElev = rect.BottomElevation;
            List<byte> result = new List<byte>();
            result.AddRange(CellIndex2Byte(rSt));
            result.AddRange(BitConverter.GetBytes(rScaleIdx));
            result.AddRange(BitConverter.GetBytes(rBtmElev));
            return result.ToArray();
        }
        public static byte[] CellIndex2Byte(CellIndex cix)
        {
            List<byte> result = new List<byte>() { Capacity = 8 };
            result.AddRange(BitConverter.GetBytes(cix.Col));
            result.AddRange(BitConverter.GetBytes(cix.Row));
            return result.ToArray();
        }
        public static byte[] CellIndex3D2Byte(CellIndex3D cix3d)
        {
            List<byte> result = new List<byte>() { Capacity = 8 };
            result.AddRange(BitConverter.GetBytes(cix3d.Col));
            result.AddRange(BitConverter.GetBytes(cix3d.Row));
            result.AddRange(BitConverter.GetBytes(cix3d.Layer));
            return result.ToArray();
        }
        /// <summary>
        /// Split accessible region by support rectangles
        /// </summary>
        /// <param name="doc">compressed voxel document</param>
        /// <param name="rect">accessible rectangle to be splitted</param>
        /// <param name="cuttingRectangle">Potential rectangles that is used for splitting accessible Regions</param>
        /// <param name="rectIndex">rectangle index of current rectangle</param>
        /// <param name="strideHeight">man stride height</param>
        /// <returns>the splitted accessible regions</returns>
        public static List<AccessibleRectangle> CutSupportARwithSupVoxelRects(CompressedVoxelDocument doc,  List<AccessibleRectangle> rect,List<VoxelRectangle> cuttingRectangle,int rectIndex,double strideHeight)
        {
            Stack<AccessibleRectangle> rect2Cut = new Stack<AccessibleRectangle>();
            foreach (var r in rect)
            {
                rect2Cut.Push(r);
            }
            foreach (var rectNear in cuttingRectangle)
            {
                if (rectNear.Index == rectIndex)//no need for self-cut
                    continue;
                List<AccessibleRectangle> newRectangleAfterCut = new List<AccessibleRectangle>();
                while (rect2Cut.Count>0)
                {
                    //determine if the cut can be done
                    var rectOut = rect2Cut.Pop();
                    //get rect boundary box
                    rectNear.Get_BoundingBox(doc, out var nearMin, out var nearMax, out var nearBtmElev, out var nearTopElev);
                    double dblElevSt = rectOut.Elevation;
                    double dblElevEd = dblElevSt + strideHeight;
                    CellIndex outMin = rectOut.Min;
                    CellIndex outMax = rectOut.Max;
                    //check if the detection range intersects the box
                    if (RangeIntersects(outMin, outMax, dblElevSt, dblElevEd, nearMin, nearMax, nearBtmElev, nearTopElev))////intersest happen
                    {
                        if (Math.Round(nearTopElev - dblElevSt, 4) == 0) //the 2 support face equal height
                        {
                            if (rectIndex < rectNear.Index)//cut
                            {
                                var newArs = CutAccessibleRectangle(rectOut, nearMin, nearMax);
                                newRectangleAfterCut.AddRange(newArs);
                            }
                            else
                            {
                                newRectangleAfterCut.Add(rectOut);
                            }
                        }
                        else //the support rectangle obstructs the current ar
                        {
                            var newArs = CutAccessibleRectangle(rectOut, nearMin, nearMax);
                            newRectangleAfterCut.AddRange(newArs);
                        }
                    }
                    else
                    {
                        newRectangleAfterCut.Add(rectOut);
                    }
                }
                //push the new accessible regions back to stack
                foreach (var newAr in newRectangleAfterCut)
                {
                    rect2Cut.Push(newAr);
                }
            }
            return rect2Cut.ToList();
        }
        /// <summary>
        /// Split accessible region by obstruction rectangles
        /// </summary>
        /// <param name="doc">compressed voxel document</param>
        /// <param name="rect">accessible rectangle to be splitted</param>
        /// <param name="cuttingRectangle"></param>
        /// <param name="rectIndex"></param>
        /// <param name="offsetIndex"></param>
        /// <param name="strdeHeight"></param>
        /// <param name="minPassingHeight"></param>
        /// <returns></returns>
        public static List<AccessibleRectangle> CutSupportARwithObsVoxelRects(CompressedVoxelDocument doc, List<AccessibleRectangle> rect, List<VoxelRectangle> cuttingRectangle, int offsetIndex,double strdeHeight,double minPassingHeight)
        {
            Stack<AccessibleRectangle> rect2Cut = new Stack<AccessibleRectangle>();
            foreach (var r in rect)
                rect2Cut.Push(r);
            foreach (var rectNear in cuttingRectangle)
            {
                List<AccessibleRectangle> newRectangleAfterCut = new List<AccessibleRectangle>();
                //determine if the cut can be done
                while (rect2Cut.Count > 0)
                {
                    var rectOut = rect2Cut.Pop();
                    //get rect boundary box
                    rectNear.Get_BoundingBox(doc, out var nearMin, out var nearMax, out var nearBtmElev, out var nearTopElev);
                    nearMin -= new CellIndex(offsetIndex, offsetIndex);
                    nearMax += new CellIndex(offsetIndex, offsetIndex);
                    double dblElevSt = rectOut.Elevation + strdeHeight;
                    double dblElevEd = rectOut.Elevation + minPassingHeight;
                    CellIndex outMin = rectOut.Min;
                    CellIndex outMax = rectOut.Max;
                    //check if the detection range intersects the box
                    if (RangeIntersects(outMin, outMax, dblElevSt, dblElevEd, nearMin, nearMax, nearBtmElev, nearTopElev))////intersest happen
                    {
                        var newArs = CutAccessibleRectangle(rectOut, nearMin, nearMax);
                        newRectangleAfterCut.AddRange(newArs);
                    }
                    else
                    {
                        newRectangleAfterCut.Add (rectOut);
                    }
                }
                //push the new accessible regions back to stack
                foreach (var newAr in newRectangleAfterCut)
                {
                    rect2Cut.Push(newAr);
                }
            }
            return rect2Cut.ToList();
        }
        private static bool RangeIntersects(CellIndex cixMin0,CellIndex cixMax0,double elevMin0,double elevMax0,CellIndex cixMin1,CellIndex cixMax1,double elevMin1,double elevMax1)
        {
            if (cixMin0.Col <= cixMax1.Col && cixMin0.Row <= cixMax1.Row
                    && cixMax0.Col >= cixMin1.Col && cixMax0.Row >= cixMin1.Row &&
                elevMin0 <=elevMax1 && elevMax0 >=elevMin1 )
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        private static List<AccessibleRectangle> CutAccessibleRectangle(AccessibleRectangle rect2Cut,CellIndex cuttingMin,CellIndex cuttingMax)
        {
            var rectMin = rect2Cut.Min;
            var rectMax = rect2Cut.Max;
            int colMax = rectMax.Col;
            int colMin = rectMin.Col;
            int rowMax = rectMax.Row;
            int rowMin = rectMin.Row;
            List<AccessibleRectangle> result = new List<AccessibleRectangle>();
            if(rectMin.Col<=cuttingMax.Col && rectMin.Row <=cuttingMax.Row && rectMax.Col >=cuttingMin.Col && rectMax.Row >=cuttingMin.Row)//can cut
            {
                int colCommonMin = Math.Max(rectMin.Col, cuttingMin.Col);
                int rowCommonMin = Math.Max(rectMin.Row, cuttingMin.Row);
                int colCommonMax = Math.Min(rectMax.Col, cuttingMax.Col); 
                int rowCommonMax=Math.Min(rectMax.Row, cuttingMax.Row);
                //POTENTIAL rectEast, the boundary is(colCommonMax+1,rowMin)-(colMax,rowmax)
                //rectNorth:(comCommonMin,rowCommonMax+1)-(colCommonMax,rowMax)
                //rectWest:(colMin,rowMin)-(colCommonMin-1,rowMax)
                //rectSouth:(colComonMin,rowMin)-(colCommonMax, rowCommonMin-1)
                List<CellIndex[]> cixRectBdry2Check = new List<CellIndex[]>();
                cixRectBdry2Check.Add(new CellIndex[2] { new CellIndex(colCommonMax + 1, rowMin), new CellIndex(colMax, rowMax) });//east
                cixRectBdry2Check.Add(new CellIndex[2] { new CellIndex(colCommonMin, rowCommonMax + 1), new CellIndex(colCommonMax, rowMax) });//north
                cixRectBdry2Check.Add(new CellIndex[2] { new CellIndex(colMin, rowMin), new CellIndex(colCommonMin - 1, rowMax) });//west
                cixRectBdry2Check.Add(new CellIndex[2] { new CellIndex(colCommonMin, rowMin), new CellIndex(colCommonMax, rowCommonMin - 1) });//south
                foreach (var cixBdry in cixRectBdry2Check)
                {
                    var cixMinTemp = cixBdry[0];
                    var cixMaxTemp = cixBdry[1];
                    var cixScale = cixMaxTemp - cixMinTemp;
                    if (cixScale.Col >= 0 && cixScale.Row >= 0) //the voxel is valid
                    {
                        AccessibleRectangle newAr = new AccessibleRectangle() { Elevation = rect2Cut.Elevation, Min = cixMinTemp, Max = cixMaxTemp };
                        result.Add(newAr);
                    }
                }
            }
            else
            {
                result.Add (rect2Cut);
            }
            return result;
        }

        public static HashSet<VoxelRectangle>  FindVoxelRectanglesWithinRanges( Dictionary<CellIndex3D,List<VoxelRectangle>> searchRange,  CellIndex min,CellIndex max,int layerSt,int layerEd)
        {
            HashSet <VoxelRectangle> rectFound=new HashSet<VoxelRectangle>();
            int colMin = min.Col;
            int colMax = max.Col;
            int rowMin = min.Row;
            int rowMax = max.Row;
           
            //organize rectangles into cells
            for (int col = colMin; col <= colMax; col ++)
            {
                for (int row = rowMin; row <= rowMax; row ++)
                {
                    for (int layer = layerSt; layer <= layerEd; layer++)
                    {
                        CellIndex3D cix = new CellIndex3D(col, row, layer);
                        if (searchRange.TryGetValue(cix, out var searchResul))
                        {
                            foreach (var rectAdj in searchResul)
                            {
                                rectFound.Add(rectAdj);
                            }
                        }
                    }
                }
            }
            return rectFound;
        }

        public static IEnumerable<AccessibleRectangle> GenereateAccessibleRectangles(VoxelRectangleManager manager,double dblStrideHeight,double dblMinPassingHeight,double dblObsAffRng)
        {
            var supRects = manager.SupRects;
            var compVoxelDoc = manager.Doc;
            var bigCellInterval = manager.CellBuffer;
            var voxSize = compVoxelDoc.VoxelSize;
            int obsAffRng = (int)Math.Ceiling(Math.Round(dblObsAffRng / voxSize, 4));
            foreach (var supRect in supRects)
            {
                foreach (var ars in GenereateAccessibleRectangles4SupportRect(supRect, manager, dblStrideHeight, dblMinPassingHeight, obsAffRng))
                {
                    yield return ars;   
                }
            }
        }

        public static IEnumerable<AccessibleRectangle> GenereateAccessibleRectangles4SupportRect(VoxelRectangle supportRect,  VoxelRectangleManager manager, double dblStrideHeight, double dblMinPassingHeight, int obsAffRng)
        {
            var compVoxelDoc = manager.Doc;
            var bigCellInterval = manager.CellBuffer;
            var voxSize = compVoxelDoc.VoxelSize;
            var cell_compVoxRect_Support = manager.Cell_compVoxRect_Support;
            var cell_compVoxRect_Obstruct = manager.Cell_compVoxRect_Obstruct;
            var supRect = supportRect;
            //get support rectangles that may affect supRect
            var scale = supRect.Get_Scale(compVoxelDoc.VoxelScale);
            double dblZ = (supRect.BottomElevation + scale.Layer) / 304.8;
            //search for potential supprot rectangles that may affect curret support
            var colMinSup = supRect.Start.Col;
            var colMaxSup = colMinSup + scale.Col;
            var rowMinSup = supRect.Start.Row;
            var rowMaxSup = rowMinSup + scale.Row;
            CellIndex cixMinSup = new CellIndex(colMinSup / bigCellInterval, rowMinSup / bigCellInterval);
            CellIndex cixMaxSup = new CellIndex(colMaxSup / bigCellInterval, rowMaxSup / bigCellInterval);
            var zMax = dblZ + dblStrideHeight;
            var rectLayerSt = (int)Math.Ceiling(dblZ / (bigCellInterval * voxSize)) - 1;
            var rectLayerEd = (int)Math.Floor(zMax / (bigCellInterval * voxSize)) + 1;
            HashSet<VoxelRectangle> supRectangleNear = LEGOVoxelTool.FindVoxelRectanglesWithinRanges(cell_compVoxRect_Support, cixMinSup, cixMaxSup, rectLayerSt, rectLayerEd);
            //create a rectangle
            AccessibleRectangle ar = new AccessibleRectangle();
            ar.Max = new CellIndex(colMaxSup, rowMaxSup);
            ar.Min = supRect.Start;
            ar.Elevation = dblZ;
            List<AccessibleRectangle> rectSup = new List<AccessibleRectangle>() { ar };
            //try cut the ars in rectGenerates
            List<AccessibleRectangle> validSupRectangles = LEGOVoxelTool.CutSupportARwithSupVoxelRects(compVoxelDoc, rectSup, supRectangleNear.ToList(), supRect.Index, dblStrideHeight);
            //search potential obstruct rectangles
            var colMinObs = colMinSup - obsAffRng;
            var rowMinObs = rowMinSup - obsAffRng;
            var colMaxObs = colMaxSup + obsAffRng;
            var rowMaxObs = rowMaxSup + obsAffRng;
            CellIndex cixMinObs = new CellIndex(colMinObs / bigCellInterval, rowMinObs / bigCellInterval);
            CellIndex cixMaxObs = new CellIndex(colMaxObs / bigCellInterval, rowMaxObs / bigCellInterval);
            var elevObsSt = dblZ + dblStrideHeight;
            var elevObsEd = dblZ + dblMinPassingHeight;
            var elevObsLayerSt = (int)Math.Ceiling(elevObsSt / (bigCellInterval * voxSize)) - 1;
            var elevObsLayerEd = (int)Math.Floor(elevObsEd / (bigCellInterval * voxSize)) + 1;
            HashSet<VoxelRectangle> obsRectangleNear = LEGOVoxelTool.FindVoxelRectanglesWithinRanges(cell_compVoxRect_Obstruct, cixMinObs, cixMaxObs, elevObsLayerSt, elevObsLayerEd);
            //try cut the ars in rectGenerates with solids
            validSupRectangles = LEGOVoxelTool.CutSupportARwithObsVoxelRects(compVoxelDoc, validSupRectangles, obsRectangleNear.ToList(), obsAffRng, dblStrideHeight, dblMinPassingHeight);
            foreach (var arect in validSupRectangles)
            {
                yield return arect;
            }
        }

        public static void FindAccessibleRectangleNeighbors(List<AccessibleRectangle> ars,double strideHeight,int bufferInterval)
        {
            Dictionary<CellIndex3D, List<AccessibleRectangle>> cix_Ars = new Dictionary<CellIndex3D, List<AccessibleRectangle>>();
            List<List<CellIndex3D>> boudaryCells = new List<List<CellIndex3D>>();
            int arIndex = 0;
            foreach (var ar in ars)
            {
                ar.Index = arIndex;
                arIndex += 1;
                CellIndex arMin = ar.Min;
                CellIndex arMax = ar.Max;
                //expand arMax by 1
                arMax += new CellIndex(1, 1);
                //get the big index of ar
                CellIndex minBigger =ConvertBigger_LeftInclusive( arMin,bufferInterval);
                CellIndex maxBigger =ConvertBigger_RightInclusive(arMax,bufferInterval);
                int layer = (int)Math.Floor(ar.Elevation / strideHeight);

                //search along boundary
                int colSt = minBigger.Col;
                int colEd = maxBigger.Col;
                int rowSt = minBigger.Row;
                int rowEd = maxBigger.Row;
                int colStBdry = (int)Math.Ceiling((double)arMin.Col / bufferInterval) - 1;
                int colEdBdry = (int)Math.Floor((double)arMax.Col/ bufferInterval) ;
                int rowStBdry = (int)Math.Ceiling((double)arMin.Row / bufferInterval) - 1;
                int rowEdBdry = (int)Math.Floor((double)arMax.Row / bufferInterval);
                // add edge bottom
                List<CellIndex3D> bdryCells = new List<CellIndex3D>();
                for(int col=colSt;col<=colEd;col++)
                {
                    CellIndex3D cix = new CellIndex3D(col, rowSt, layer);
                    bdryCells.Add(new CellIndex3D(col,rowStBdry,layer));
                    if(!cix_Ars.ContainsKey(cix))
                    {
                        cix_Ars.Add (cix, new List<AccessibleRectangle>() { ar});
                    }
                    else
                    {
                        cix_Ars[cix].Add(ar);
                    }
                }
                //add edge right
                for (int row = rowSt; row <=rowEd; row++)
                {
                    CellIndex3D cix = new CellIndex3D(colEd, row, layer);
                    bdryCells.Add(new CellIndex3D(colEdBdry,row,layer));
                    if (!cix_Ars.ContainsKey(cix))
                    {
                        cix_Ars.Add(cix, new List<AccessibleRectangle>() { ar });
                    }
                    else
                    {
                        cix_Ars[cix].Add(ar);
                    }
                }
                // add edge top
                for (int col = colEd; col >= colSt; col--)
                {
                    CellIndex3D cix = new CellIndex3D(col, rowEd, layer);
                    bdryCells.Add(new CellIndex3D(col, rowEdBdry, layer));
                    if (!cix_Ars.ContainsKey(cix))
                    {
                        cix_Ars.Add(cix, new List<AccessibleRectangle>() { ar });
                    }
                    else
                    {
                        cix_Ars[cix].Add(ar);
                    }
                }
                //add edge left
                for (int row = rowEd; row >=rowSt; row--)
                {
                    CellIndex3D cix = new CellIndex3D(colSt, row, layer);
                    bdryCells.Add(new CellIndex3D(colStBdry, row, layer));
                    if (!cix_Ars.ContainsKey(cix))
                    {
                        cix_Ars.Add(cix, new List<AccessibleRectangle>() { ar });
                    }
                    else
                    {
                        cix_Ars[cix].Add(ar);
                    }
                }
                boudaryCells.Add (bdryCells);
            }
            //search ars
            for(int i=0;i<=ars.Count -1;i++)
            {
                var ar = ars[i];
                var arBdryCells=boudaryCells[i];
                HashSet<AccessibleRectangle> rectNear = new HashSet<AccessibleRectangle>();
                foreach (var cixOriginal in arBdryCells)
                {
                    for(int offLayer=-1;offLayer <=1;offLayer ++)
                    {
                        CellIndex3D cix = new CellIndex3D(cixOriginal.Col, cixOriginal.Row, cixOriginal.Layer + offLayer);
                        if (cix_Ars.TryGetValue(cix, out var rectsAdj))
                        {
                            foreach (var arAdj in rectsAdj)
                            {
                                if (arAdj != ar)
                                {
                                    rectNear.Add(arAdj);
                                }
                            }
                        }
                    }
                   
                }
                foreach (var rectNearTemp in rectNear)
                {
                    if(NavigableTo(ar,rectNearTemp,strideHeight))
                    {
                        ar.AdjacentRectangles.Add(rectNearTemp);
                    }
                }
            }
        }
        
        
        
        private static CellIndex ConvertBigger_LeftInclusive(CellIndex cix, int bufferInterval)
        {
            int col = (int)Math.Floor(cix.Col / (double)bufferInterval);
            int row= (int)Math.Floor(cix.Row / (double)bufferInterval);
            return new CellIndex(col,row);
        }
        private static CellIndex ConvertBigger_RightInclusive(CellIndex cix, int bufferInterval)
        {
            int col = (int)Math.Ceiling(cix.Col / (double)bufferInterval)-1;
            int row = (int)Math.Ceiling(cix.Row / (double)bufferInterval)-1;
            return new CellIndex(col, row);
        }
        private static bool NavigableTo(AccessibleRectangle source, AccessibleRectangle target,double strideHeight)
        {
           

            if (Math.Abs (source.Elevation -target.Elevation)<=strideHeight)
            {
                CellIndex minS = source.Min;
                CellIndex maxS = source.Max + new CellIndex(1, 1);
                CellIndex minT = target.Min;
                CellIndex maxT = target.Max + new CellIndex(1, 1);
                CellIndex disMaxT_MinS = maxT - minS;
                CellIndex disMaxS_MinT = maxS - minT;

                int colMin0 = minS.Col;
                int rowMin0 = minS.Row;
                int colMax0 = maxS.Col;
                int rowMax0 = maxS.Row;

                int colMin1 = minT.Col;
                int rowMin1 = minT.Row;
                int colMax1 = maxT.Col;
                int rowMax1 = maxT.Row;

                if((disMaxS_MinT.Col >=0 && disMaxT_MinS.Col >=0)&& disMaxS_MinT.Row>0 && disMaxT_MinS.Row >0)
                {
                    return true;
                }
                
                if ((disMaxS_MinT.Col >0 && disMaxT_MinS.Col > 0) && disMaxS_MinT.Row >= 0 && disMaxT_MinS.Row >= 0)
                {
                    return true;
                }

                return false;
            }
            return false;
        }

        
    }

    public class VoxelRectangleManager
    {
        public CompressedVoxelDocument Doc { get; set; }
        public Dictionary<CellIndex3D, List<VoxelRectangle>> Cell_compVoxRect_Support;
        public  Dictionary<CellIndex3D, List<VoxelRectangle>> Cell_compVoxRect_Obstruct;
        public  List<VoxelRectangle> SupRects = new List<VoxelRectangle>();
        public  List<VoxelRectangle> ObsRects = new List<VoxelRectangle>();
        public int CellBuffer { get; set; }
        public VoxelRectangleManager(CompressedVoxelDocument doc,int cellBuffer)
        {
            //param 
            this.CellBuffer=cellBuffer;
            this.Doc = doc;
            //use a dictionary to store element spatical occupy situation
            int bigCellInterval = 10;// the buffer is 10 times that of the voxel size
            Cell_compVoxRect_Support = new Dictionary<CellIndex3D, List<VoxelRectangle>>();
            Cell_compVoxRect_Obstruct = new Dictionary<CellIndex3D, List<VoxelRectangle>>();
            SupRects = new List<VoxelRectangle>();
            ObsRects = new List<VoxelRectangle>();
            Vec3 origin = Doc.Origin;
            double voxSize = Doc.VoxelSize;
            //change the offset to int
            foreach (var elem in doc.Elements)
            {
                if (!elem.IsActive)
                {
                    continue;
                }
                foreach (var rect in elem.VoxelRectangles)
                {
                    if (elem.IsSupportElement)
                    {
                        rect.Index = SupRects.Count;
                        SupRects.Add(rect);
                    }
                    if (elem.IsObstructElement)
                    {
                        ObsRects.Add(rect);
                    }
                    rect.Get_BoundingBox(Doc, out CellIndex min, out CellIndex max, out double bottomElev, out double topElev);
                    int colStOriginal = min.Col;
                    int colEdOriginal = max.Col;
                    int rowStOriginal = min.Row;
                    int rowEdOriginal = max.Row;
                    int layerSt = (int)Math.Floor((bottomElev - origin.Z) / (bigCellInterval * voxSize));
                    int layerEd = (int)Math.Floor((topElev - origin.Z) / (bigCellInterval * voxSize));
                    //organize rectangles into cells
                    int colSt = colStOriginal / bigCellInterval;
                    int colEd = colEdOriginal / bigCellInterval;
                    int rowSt = rowStOriginal / bigCellInterval;
                    int rowEd = rowEdOriginal / bigCellInterval;
                    for (int col = colSt; col <= colEd; col++)
                    {
                        for (int row = rowSt; row <= rowEd; row++)
                        {
                            for (int layer = layerSt; layer <= layerEd; layer++)
                            {
                                CellIndex3D bufferCell3D = new CellIndex3D(col, row, layer);

                                if (elem.IsSupportElement)
                                {
                                    if (!Cell_compVoxRect_Support.ContainsKey(bufferCell3D))
                                    {
                                        Cell_compVoxRect_Support.Add(bufferCell3D, new List<VoxelRectangle>());
                                    }
                                    Cell_compVoxRect_Support[bufferCell3D].Add(rect);
                                }
                                if (elem.IsObstructElement)
                                {
                                    if (!Cell_compVoxRect_Obstruct.ContainsKey(bufferCell3D))
                                    {
                                        Cell_compVoxRect_Obstruct.Add(bufferCell3D, new List<VoxelRectangle>());
                                    }
                                    Cell_compVoxRect_Obstruct[bufferCell3D].Add(rect);
                                }
                            }
                        }
                    }

                }
            }
        }
    }
    #endregion
}
