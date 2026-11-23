// Skaitymas: vaizdo (view=main | pjūvio raidė | D<pos> detalės vaizdas) detalių kraštai vaizdo koordinatėmis "pos|xmin|xmax|ymin|ymax", skylių centrai; verts=<pos> – tos detalės kampų taškai
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
var sb = new System.Text.StringBuilder();
sb.Append("scale " + vw.Attributes.Scale + "\n");
var pe2 = vw.GetAllObjects(new[] { typeof(TSD.Part) });
while (pe2.MoveNext()) {
  var dp = pe2.Current as TSD.Part; var mp = dp == null ? null : model.SelectModelObject(dp.ModelIdentifier) as TSM.Part; if (mp == null) continue;
  string pos = ""; mp.GetReportProperty("PART_POS", ref pos);
  var s = mp.GetSolid();
  sb.Append(pos + " " + mp.Profile.ProfileString + "|" + s.MinimumPoint.X.ToString("0.#", CI) + "|" + s.MaximumPoint.X.ToString("0.#", CI) + "|" + s.MinimumPoint.Y.ToString("0.#", CI) + "|" + s.MaximumPoint.Y.ToString("0.#", CI) + "\n");
  var be = mp.GetBolts();
  while (be.MoveNext()) { var bg = be.Current as TSM.BoltGroup; if (bg == null) continue; bg.Select();
    sb.Append("  holes:"); foreach (T3D.Point p in bg.BoltPositions) sb.Append(" " + p.X.ToString("0.#", CI) + "," + p.Y.ToString("0.#", CI)); sb.Append("\n"); }
  if (pos == "{{verts}}") {
    var set = new System.Collections.Generic.SortedSet<string>();
    var fe = s.GetFaceEnumerator();
    while (fe.MoveNext()) { var le = fe.Current.GetLoopEnumerator(); while (le.MoveNext()) { var ve2 = le.Current.GetVertexEnumerator(); while (ve2.MoveNext()) { var p = ve2.Current as T3D.Point; if (p != null) set.Add(p.X.ToString("0", CI) + "," + p.Y.ToString("0", CI)); } } }
    sb.Append("  verts: " + string.Join(" ", set) + "\n");
  }
}
wph.SetCurrentTransformationPlane(gp);
sb.ToString()
