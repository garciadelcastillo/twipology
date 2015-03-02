using Rhino;
using Rhino.Geometry;
using Rhino.DocObjects;
using Rhino.Collections;

using GH_IO;
using GH_IO.Serialization;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Linq;
using System.Data;
using System.Drawing;
using System.Reflection;
using System.Collections;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Runtime.InteropServices;



/// <summary>
/// This class will be instantiated on demand by the Script component.
/// </summary>
public class Script_Instance : GH_ScriptInstance
{
#region Utility functions
  /// <summary>Print a String to the [Out] Parameter of the Script component.</summary>
  /// <param name="text">String to print.</param>
  private void Print(string text) { __out.Add(text); }
  /// <summary>Print a formatted String to the [Out] Parameter of the Script component.</summary>
  /// <param name="format">String format.</param>
  /// <param name="args">Formatting parameters.</param>
  private void Print(string format, params object[] args) { __out.Add(string.Format(format, args)); }
  /// <summary>Print useful information about an object instance to the [Out] Parameter of the Script component. </summary>
  /// <param name="obj">Object instance to parse.</param>
  private void Reflect(object obj) { __out.Add(GH_ScriptComponentUtilities.ReflectType_CS(obj)); }
  /// <summary>Print the signatures of all the overloads of a specific method to the [Out] Parameter of the Script component. </summary>
  /// <param name="obj">Object instance to parse.</param>
  private void Reflect(object obj, string method_name) { __out.Add(GH_ScriptComponentUtilities.ReflectType_CS(obj, method_name)); }
#endregion

#region Members
  /// <summary>Gets the current Rhino document.</summary>
  private RhinoDoc RhinoDocument;
  /// <summary>Gets the Grasshopper document that owns this script.</summary>
  private GH_Document GrasshopperDocument;
  /// <summary>Gets the Grasshopper script component that owns this script.</summary>
  private IGH_Component Component;
  /// <summary>
  /// Gets the current iteration count. The first call to RunScript() is associated with Iteration==0.
  /// Any subsequent call within the same solution will increment the Iteration count.
  /// </summary>
  private int Iteration;
#endregion

  /// <summary>
  /// This procedure contains the user code. Input parameters are provided as regular arguments,
  /// Output parameters as ref arguments. You don't have to assign output parameters,
  /// they will have a default value.
  /// </summary>
  private void RunScript(Surface Surf, int RowNum, int ColumnNum, double HorizontalSeamIndent, double VerticalSeamIndent, bool IsFirstRowHorizontal, double SeamOffset, double WallThickness, int PatternType, ref object BaseUVPoints, ref object FrontUVPoints, ref object BackUVPoints, ref object BaseUVFrames, ref object FrontUVFrames, ref object BackUVFrames, ref object FrontPoints, ref object BackPoints, ref object ProjectionVectors, ref object BaseBrickUVFrames, ref object BackBrickUVFrames, ref object BrickWireframe, ref object BrickMesh)
  {
    // REMEMBER: U PARAM DRIVES COLUMNS, V PARAM DRIVES ROWS
    int i, j, m, n;
    int C = 2 * ColumnNum; int R = RowNum;
    Surface s = Surf;
    if (C < 2 || R < 2) return;
    double du = 1 / (double) C;
    double dv = 1 / (double) R;
    double indv = dv * HorizontalSeamIndent;
    double indh = du * VerticalSeamIndent;   // new vertical indent introduction
    double off = SeamOffset;
    int ptyp = PatternType;

    Interval zerone = new Interval(0.0, 1.0);
    s.SetDomain(0, zerone);
    s.SetDomain(1, zerone);

    DataTree<Brick> bricks = new DataTree<Brick>();

    DataTree<Point2d> BaseUVP = new DataTree<Point2d>();
    DataTree<Point2d> FrontUVP = new DataTree<Point2d>();
    DataTree<Point2d> BackUVP = new DataTree<Point2d>();
    DataTree<Plane> BaseUVF = new DataTree<Plane>();
    DataTree<Plane> FrontUVF = new DataTree<Plane>();
    DataTree<Plane> BackUVF = new DataTree<Plane>();
    DataTree<Point3d> FrontP = new DataTree<Point3d>();
    DataTree<Point3d> BackP = new DataTree<Point3d>();
    DataTree<Vector3d> ProjectionV = new DataTree<Vector3d>();

    DataTree<Mesh> BrickMeshes = new DataTree<Mesh>();
    DataTree<Plane> BrickPlaneReference = new DataTree<Plane>();
    DataTree<Plane> BrickPlaneReferenceBack = new DataTree<Plane>();
    DataTree<Polyline> BrickWireframes = new DataTree<Polyline>();

    // ANALYSING THE SURFACE INFORMATION
    for (i = 0; i <= R; i++) {
      GH_Path path = new GH_Path(i);

      for (j = 0; j <= C; j++) {
        Point2d buv, fuv, bkuv;
        buv = new Point2d(j * du, i * dv);
        BaseUVP.Add(buv, path);

        // different pattern variations (this could probably be optimized or made a function)
        switch (PatternType) {

          default:
            if (i == 0 || i == R || j == 0 || j == C ) {
              if ((i == 0 || i == R) && (j == 0 || j == C)) {
                fuv = new Point2d(j * du, i * dv);
                bkuv = new Point2d(j * du, i * dv);
              } else if (i == 0 || i == R) {
                fuv = new Point2d(j * du - indh + 2 * indh * (i % 2), i * dv);
                bkuv = new Point2d(j * du + indh - 2 * indh * (i % 2), i * dv);
              } else {
                fuv = new Point2d(j * du, i * dv - indv + 2 * indv * (j % 2));
                bkuv = new Point2d(j * du, i * dv + indv - 2 * indv * (j % 2));
              }
            } else {
              fuv = new Point2d(j * du - indh + 2 * indh * (i % 2), i * dv - indv + 2 * indv * (j % 2));
              bkuv = new Point2d(j * du + indh - 2 * indh * (i % 2), i * dv + indv - 2 * indv * (j % 2));
            }
            break;

          case (1):
            if (i == 0 || i == R || j == 0 || j == C ) {
              if ((i == 0 || i == R) && (j == 0 || j == C)) {
                fuv = new Point2d(j * du, i * dv);
                bkuv = new Point2d(j * du, i * dv);
              } else if (i == 0 || i == R) {
                fuv = new Point2d(j * du + indh + 2 * indh * (i % 2), i * dv);
                bkuv = new Point2d(j * du - indh - 2 * indh * (i % 2), i * dv);
              } else {
                fuv = new Point2d(j * du, i * dv - indv + 2 * indv * (j % 2));
                bkuv = new Point2d(j * du, i * dv + indv - 2 * indv * (j % 2));
              }
            } else {
              fuv = new Point2d(j * du + indh + 2 * indh * (i % 2), i * dv - indv + 2 * indv * (j % 2));
              bkuv = new Point2d(j * du - indh - 2 * indh * (i % 2), i * dv + indv - 2 * indv * (j % 2));
            }
            break;

          case (2):
            if (i == 0 || i == R || j == 0 || j == C ) {
              if ((i == 0 || i == R) && (j == 0 || j == C)) {
                fuv = new Point2d(j * du, i * dv);
                bkuv = new Point2d(j * du, i * dv);
              } else if (i == 0 || i == R) {
                fuv = new Point2d(j * du - indh + 2 * indh * (i % 2), i * dv);
                bkuv = new Point2d(j * du + indh - 2 * indh * (i % 2), i * dv);
              } else {
                fuv = new Point2d(j * du, i * dv - indv - 2 * indv * (j % 2));
                bkuv = new Point2d(j * du, i * dv + indv + 2 * indv * (j % 2));
              }
            } else {
              fuv = new Point2d(j * du - indh + 2 * indh * (i % 2), i * dv - indv - 2 * indv * (j % 2));
              bkuv = new Point2d(j * du + indh - 2 * indh * (i % 2), i * dv + indv + 2 * indv * (j % 2));
            }
            break;

          case (3):
            if (i == 0 || i == R || j == 0 || j == C ) {
              if ((i == 0 || i == R) && (j == 0 || j == C)) {
                fuv = new Point2d(j * du, i * dv);
                bkuv = new Point2d(j * du, i * dv);
              } else if (i == 0 || i == R) {
                fuv = new Point2d(j * du - indh + 2 * indh * (i % 2), i * dv);
                bkuv = new Point2d(j * du + indh - 2 * indh * (i % 2), i * dv);
              } else {
                fuv = new Point2d(j * du, i * dv - indv + 2 * indv * (j % 2));
                bkuv = new Point2d(j * du, i * dv + indv - 2 * indv * (j % 2));
              }
            } else {
              fuv = new Point2d(j * du - indh + 2 * indh * (j % 2), i * dv - indv + 2 * indv * (i % 2));
              bkuv = new Point2d(j * du + indh - 2 * indh * (j % 2), i * dv + indv - 2 * indv * (i % 2));
            }
            break;

          case (4):
            if (i == 0 || i == R || j == 0 || j == C ) {
              if ((i == 0 || i == R) && (j == 0 || j == C)) {
                fuv = new Point2d(j * du, i * dv);
                bkuv = new Point2d(j * du, i * dv);
              } else if (i == 0 || i == R) {
                fuv = new Point2d(j * du - indh + 2 * indh * (i % 2), i * dv);
                bkuv = new Point2d(j * du + indh - 2 * indh * (i % 2), i * dv);
              } else {
                fuv = new Point2d(j * du, i * dv - indv + 2 * indv * (j % 2));
                bkuv = new Point2d(j * du, i * dv + indv - 2 * indv * (j % 2));
              }
            } else {
              fuv = new Point2d(j * du - indh + 2 * indh * (j % 2), i * dv - indv + 2 * indv * (j % 2));
              bkuv = new Point2d(j * du + indh - 2 * indh * (i % 2), i * dv + indv - 2 * indv * (i % 2));
            }
            break;

          case (5):  // THIS IS PROBABLY THE GOOD ONE!
            if (i == 0 || i == R || j == 0 || j == C ) {
              if ((i == 0 || i == R) && (j == 0 || j == C)) {
                fuv = new Point2d(j * du, i * dv);
                bkuv = new Point2d(j * du, i * dv);
              } else if (i == 0 || i == R) {
                fuv = new Point2d(j * du - indh + 2 * indh * (i % 2), i * dv);
                bkuv = new Point2d(j * du + indh - 2 * indh * (i % 2), i * dv);
              } else {
                fuv = new Point2d(j * du, i * dv - indv + 2 * indv * ((i + j) % 2));
                bkuv = new Point2d(j * du, i * dv + indv - 2 * indv * ((i + j) % 2));
              }
            } else {
              fuv = new Point2d(j * du - indh + 2 * indh * (i % 2), i * dv - indv + 2 * indv * ((i + j) % 2));
              bkuv = new Point2d(j * du + indh - 2 * indh * (i % 2), i * dv + indv - 2 * indv * ((i + j) % 2));
            }
            break;

          case (6):  // SLIGHT VARIATION OF CASE 5
            if (i == 0 || i == R || j == 0 || j == C ) {
              if ((i == 0 || i == R) && (j == 0 || j == C)) {
                fuv = new Point2d(j * du, i * dv);
                bkuv = new Point2d(j * du, i * dv);
              } else if (i == 0 || i == R) {
                fuv = new Point2d(j * du - indh + 2 * indh * (i % 2), i * dv);
                bkuv = new Point2d(j * du + indh - 2 * indh * (i % 2), i * dv);
              } else {
                fuv = new Point2d(j * du, i * dv - indv + 2 * indv * ((i + j) % 2));
                bkuv = new Point2d(j * du, i * dv + indv - 2 * indv * ((i + j) % 2));
              }
            } else {
              fuv = new Point2d(j * du - indh + 2 * indh * ((i + j) % 2), i * dv - indv + 2 * indv * ((i + j) % 2));
              bkuv = new Point2d(j * du + indh - 2 * indh * ((i + j) % 2), i * dv + indv - 2 * indv * ((i + j) % 2));
            }
            break;

        }

        FrontUVP.Add(fuv, path);
        BackUVP.Add(bkuv, path);

        Plane bp, fp, bkp;
        s.FrameAt(buv.X, buv.Y, out bp);
        s.FrameAt(fuv.X, fuv.Y, out fp);
        s.FrameAt(bkuv.X, bkuv.Y, out bkp);
        if (IsFirstRowHorizontal && i == 0) {
          bp = new Plane(bp.Origin, bp.XAxis, Vector3d.ZAxis);
          fp = new Plane(fp.Origin, fp.XAxis, Vector3d.ZAxis);
          bkp = new Plane(bkp.Origin, bkp.XAxis, Vector3d.ZAxis);
        }
        BaseUVF.Add(bp, path);
        FrontUVF.Add(fp, path);
        BackUVF.Add(bkp, path);

        Point3d fpoint = fp.PointAt(0, 0, off);
        Point3d bpoint = bkp.PointAt(0, 0, -off);
        FrontP.Add(fpoint, path);
        BackP.Add(bpoint, path);

        Vector3d pv = bpoint - fpoint;
        ProjectionV.Add(pv, path);
      }
    }

    // DataTree<Point3d> tempTree = new DataTree<Point3d>();
    DataTree<Plane> bfp = new DataTree<Plane>();

    // CREATING THE BRICKS
    int brickcounter = 0;
    for (i = 0; i < R; i++) {  // Loop per ROWS
      GH_Path path = new GH_Path(i);

      if (i % 2 == 0) {   // if rownum is even, do complete bricks
        for (j = 0; j < C; j += 2) {  // Loop per COLUMNS
          List<Point3d> pc = new List<Point3d>();  // 6 points on surface (PointsCenter)
          List<Point3d> pf = new List<Point3d>();  // 6 points in front of the surface (PointsFront)
          List<Point3d> pb = new List<Point3d>();  // 6 points on the back of the surface (PointsBack)
          List<Point2d> puv = new List<Point2d>();  // 6 UVpoints surrounding the center reference frame

          for (n = 0; n < 2; n++) {
            GH_Path pathRows = new GH_Path(i + n);
            for (m = 0; m < 3; m++) {
              // GENERATION OF THE POINT LISTS TO PASS ON TO THE BRICK CONSTRUCTOR
              Point3d ppc = BaseUVF[pathRows, j + m].Origin;
              pc.Add(ppc);

              Point3d ppf = FrontP[pathRows, j + m];
              pf.Add(ppf);

              Point3d ppb = BackP[pathRows, j + m];
              pb.Add(ppb);

              Point2d ppuv = BaseUVP[pathRows, j + m];
              puv.Add(ppuv);
            }
          }

          // GENERATION OF THE SURFACE CENTERFRAME (TO BE USED AS ORIENTATION REFERENCE)
          double u = 0.0; double v = 0.0;
          foreach(Point2d p in puv) {
            u += p.X; v += p.Y;
          }
          Plane centerreferenceplane;
          s.FrameAt(u / puv.Count, v / puv.Count, out centerreferenceplane);

          Brick b = new Brick(pc, pf, pb, centerreferenceplane, WallThickness, true);  // To add constructor information
          b.GenerateMesh();
          b.GeneratePolylines(pf, pb);  // generates front and back faces polylines
          bfp.Add(b.plF, path);
          bricks.Add(b, path);
          BrickMeshes.Add(b.m, path);
          BrickPlaneReference.Add(b.plR, path);

          // we will use refplane and move it to the center of the backbestfitplane plB of the brick
          Plane backreferenceplane = new Plane(centerreferenceplane);
          backreferenceplane.Origin = b.pB;
          BrickPlaneReferenceBack.Add(backreferenceplane, path);

          GH_Path pathb = new GH_Path(brickcounter);
          BrickWireframes.Add(b.plineF, pathb);
          BrickWireframes.Add(b.plineB, pathb);

          brickcounter++;
        }
      } else {  // if not, create the staggered brick assembly
        for (j = 0; j <= C - 1; j += 2) {  // Loop per COLUMNS
          List<Point3d> pc = new List<Point3d>();  // 6 points on surface (PointsCenter)
          List<Point3d> pf = new List<Point3d>();  // 6 points in front of the surface (PointsFront)
          List<Point3d> pb = new List<Point3d>();  // 6 points on the back of the surface (PointsBack)
          List<Point2d> puv = new List<Point2d>();  // 6 UVpoints surrounding the center reference frame

          int it;
          if (j == 0 || j == C - 1) it = 2; //if border situation, just create 8 point brick
          else it = 3;

          for (n = 0; n < 2; n++) {
            GH_Path pathRows = new GH_Path(i + n);
            for (m = 0; m < it; m++) {
              // GENERATION OF THE POINT LISTS TO PASS ON TO THE BRICK CONSTRUCTOR
              Point3d ppc = BaseUVF[pathRows, j + m].Origin;
              pc.Add(ppc);

              Point3d ppf = FrontP[pathRows, j + m];
              pf.Add(ppf);

              Point3d ppb = BackP[pathRows, j + m];
              pb.Add(ppb);

              Point2d ppuv = BaseUVP[pathRows, j + m];
              puv.Add(ppuv);
            }
          }
          // GENERATION OF THE SURFACE CENTERFRAME (TO BE USED AS ORIENTATION REFERENCE)
          double u = 0.0; double v = 0.0;
          foreach(Point2d p in puv) {
            u += p.X; v += p.Y;
          }
          Plane centerreferenceplane;
          s.FrameAt(u / puv.Count, v / puv.Count, out centerreferenceplane);

          // this gives too much deviation from back face
          /*Vector3d backvector = -off * centerreferenceplane.ZAxis;
          Plane backreferenceplane = new Plane(centerreferenceplane);
          backreferenceplane.Translate(backvector);*/

          Brick b;
          if (j == 0 || j == C - 1) {  // if j is first or last,
            j -= 1; //don't count 2 units+
            b = new Brick(pc, pf, pb, centerreferenceplane, WallThickness, false);  // create brick of size 1 unit
          } else b = new Brick(pc, pf, pb, centerreferenceplane, WallThickness, true);  // create brick of size 2 units
          b.GenerateMesh();
          b.GeneratePolylines(pf, pb);  // generates front and back faces polylines
          bfp.Add(b.plF, path);
          bricks.Add(b, path);
          BrickMeshes.Add(b.m, path);
          BrickPlaneReference.Add(b.plR, path);

          // we will use refplane and move it to the center of the backbestfitplane plB of the brick
          Plane backreferenceplane = new Plane(centerreferenceplane);
          backreferenceplane.Origin = b.pB;
          BrickPlaneReferenceBack.Add(backreferenceplane, path);

          GH_Path pathb = new GH_Path(brickcounter);
          BrickWireframes.Add(b.plineF, pathb);
          BrickWireframes.Add(b.plineB, pathb);

          brickcounter++;
        }
      }
    }

    BaseUVPoints = BaseUVP;
    FrontUVPoints = FrontUVP;
    BackUVPoints = BackUVP;
    BaseUVFrames = BaseUVF;
    FrontUVFrames = FrontUVF;
    BackUVFrames = BackUVF;
    FrontPoints = FrontP;
    BackPoints = BackP;
    ProjectionVectors = ProjectionV;

    BaseBrickUVFrames = BrickPlaneReference;
    BackBrickUVFrames = BrickPlaneReferenceBack;
    BrickWireframe = BrickWireframes;
    BrickMesh = BrickMeshes;


  }

  // <Custom additional code> 

  class Brick {
    public int np;  // Number of defining points per plane (6 or 4)
    public Point3d pC, pB;  //, pF;  // Centerpoints of planes
    public Plane plR;  // center reference plane on surface (for orientation)
    public Plane plC, plF, plB;  // Bestfit planes Center, Front and Back
    public double deviation;
    public List<Point3d> pt;
    public Mesh m;
    public bool doubleunit;  // true when brick is composed of 12 vertices, false for 8
    public Polyline plineF, plineB;  // polylines defining front and back faces

    public Brick(List<Point3d> pc, List<Point3d> pf, List<Point3d> pb, Plane pr, double d, bool du) {
      plR = pr;
      np = pc.Count;
      doubleunit = du;

      // MOST OF THIS IS OUTDATED, WAS INTENDED TO BE IMPLEMENTED
      // TO HAVE PLANAR FACES...


      // CENTER POINT AND CENTER BESTFITPLANE
      double x = 0.0; double y = 0.0; double z = 0.0;
      foreach(Point3d p in pc) {
        x += p.X; y += p.Y; z += p.Z;
      }
      pC = new Point3d(x / np, y / np, z / np);   // the average point
      Plane.FitPlaneToPoints(pc, out plC, out deviation);
      plC.Origin = pC;

      x = 0.0; y = 0.0; z = 0.0;
      foreach(Point3d p in pb) {
        x += p.X; y += p.Y; z += p.Z;
      }
      pB = new Point3d(x / np, y / np, z / np);   // the average point

      // IF PLANE IS BACKWARDS, FLIP IT
      if (Vector3d.Multiply(plC.ZAxis, plR.ZAxis) < 0) plC.Flip();

      plF = new Plane(plC);
      plF.Translate(d * plC.ZAxis);
      plB = new Plane(plC);
      plB.Translate(-d * plC.ZAxis);

      // GENERATE LIST OF POINTS
      pt = new List<Point3d>();
      pt.AddRange(pf);
      pt.AddRange(pb);
    }

    public void GenerateMesh() {
      m = new Mesh();
      m.Vertices.AddVertices(pt);

      if (doubleunit) {
        m.Faces.AddFace(0, 1, 4, 3);
        m.Faces.AddFace(1, 2, 5, 4);
        m.Faces.AddFace(2, 8, 11, 5);
        m.Faces.AddFace(8, 7, 10, 11);
        m.Faces.AddFace(7, 6, 9, 10);
        m.Faces.AddFace(6, 0, 3, 9);
        m.Faces.AddFace(3, 4, 10, 9);
        m.Faces.AddFace(4, 5, 11, 10);
        m.Faces.AddFace(0, 6, 7, 1);
        m.Faces.AddFace(1, 7, 8, 2);
      } else {
        m.Faces.AddFace(0, 1, 3, 2);
        m.Faces.AddFace(1, 5, 7, 3);
        m.Faces.AddFace(5, 4, 6, 7);
        m.Faces.AddFace(4, 0, 2, 6);
        m.Faces.AddFace(2, 3, 7, 6);
        m.Faces.AddFace(0, 4, 5, 1);
      }
      m.Normals.ComputeNormals();
      m.FaceNormals.ComputeFaceNormals();
    }

    public void GeneratePolylines(List<Point3d> pf, List<Point3d> pb) {
      plineF = new Polyline();
      plineB = new Polyline();
      if (doubleunit) {
        plineF.Add(pf[0]);
        plineF.Add(pf[1]);
        plineF.Add(pf[2]);
        plineF.Add(pf[5]);
        plineF.Add(pf[4]);
        plineF.Add(pf[3]);
        plineF.Add(pf[0]);

        plineB.Add(pb[0]);
        plineB.Add(pb[1]);
        plineB.Add(pb[2]);
        plineB.Add(pb[5]);
        plineB.Add(pb[4]);
        plineB.Add(pb[3]);
        plineB.Add(pb[0]);

      } else {
        plineF.Add(pf[0]);
        plineF.Add(pf[1]);
        plineF.Add(pf[3]);
        plineF.Add(pf[2]);
        plineF.Add(pf[0]);

        plineB.Add(pb[0]);
        plineB.Add(pb[1]);
        plineB.Add(pb[3]);
        plineB.Add(pb[2]);
        plineB.Add(pb[0]);
      }


    }
  }
  // </Custom additional code> 

  private List<string> __err = new List<string>(); //Do not modify this list directly.
  private List<string> __out = new List<string>(); //Do not modify this list directly.
  private RhinoDoc doc = RhinoDoc.ActiveDoc;       //Legacy field.
  private IGH_ActiveObject owner;                  //Legacy field.
  private int runCount;                            //Legacy field.

}