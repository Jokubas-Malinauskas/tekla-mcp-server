// Matmenys vienu kvietimu vaizde (view=main | pjūvio raidė | D<pos>): sets = "up;dist;x,y x,y ...|a;dist;ox,oy x1,y1 x2,y2" (up: 0,-1 / 1,0 ...; a = kampas; dist popieriaus mm)
using TSD = Tekla.Structures.Drawing;
using TSM = Tekla.Structures.Model;
using T3D = Tekla.Structures.Geometry3d;
var model = new TSM.Model();
var dh = new TSD.DrawingHandler();
var d = dh.GetActiveDrawing();
if (d == null || d.Mark != "{{mark}}") return "WRONG DRAWING";
string want = "{{view}}";
TSD.View vw = null;
var ve = d.GetSheet().GetAllObjects(new[] { typeof(TSD.View) });
while (ve.MoveNext() && vw == null) {
  var v = ve.Current as TSD.View; if (v == null) continue;
  var parts = new System.Collections.Generic.List<TSM.Part>();
  var pe = v.GetAllObjects(new[] { typeof(TSD.Part) });
  while (pe.MoveNext()) { var dp = pe.Current as TSD.Part; var mp = dp == null ? null : model.SelectModelObject(dp.ModelIdentifier) as TSM.Part; if (mp != null) parts.Add(mp); }
  if (want == "main" && v.ViewType != TSD.View.ViewTypes.SectionView && parts.Count > 1) vw = v;
  else if (v.ViewType == TSD.View.ViewTypes.SectionView && v.Name == want) vw = v;
  else if (want.StartsWith("D") && parts.Count == 1) { string pos = ""; parts[0].GetReportProperty("PART_POS", ref pos); if ("D" + pos == want) vw = v; }
}
if (vw == null) return "view not found: " + want;
var wph = model.GetWorkPlaneHandler(); var gp = wph.GetCurrentTransformationPlane();
vw.Select();
wph.SetCurrentTransformationPlane(new TSM.TransformationPlane(vw.DisplayCoordinateSystem));
var CI = System.Globalization.CultureInfo.InvariantCulture;
wph.SetCurrentTransformationPlane(gp);
var dsh = new TSD.StraightDimensionSetHandler();
var da = new TSD.StraightDimensionSet.StraightDimensionSetAttributes(null, "M.T matmenys");
da.Placing.Placing = TSD.DimensionSetBaseAttributes.Placings.Fixed;
double sc = vw.Attributes.Scale; int n = 0;
System.Func<string, T3D.Point> P = (t) => { var a = t.Split(','); return new T3D.Point(double.Parse(a[0], CI), double.Parse(a[1], CI), 0); };
foreach (var set in "{{sets}}".Split('|')) {
  var f = set.Split(';'); if (f.Length < 3) continue;
  var pts = f[2].Trim().Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries).Select(P).ToList();
  double dist = double.Parse(f[1], CI) * sc;
  if (f[0] == "a") {
    var ad = new TSD.AngleDimension(vw, pts[0], pts[1], pts[2], dist / sc, new TSD.AngleDimensionAttributes("M.T laipsniai"));
    if (ad.Insert()) n++;
  } else {
    var up = P(f[0]); var pl = new TSD.PointList(); foreach (var p in pts) pl.Add(p);
    if (dsh.CreateDimensionSet(vw, pl, new T3D.Vector(up.X, up.Y, 0), dist, da) != null) n++;
  }
}
d.CommitChanges();
"created " + n
