// Visa atramos (vamzdis + viršutinė plokštelė + padas) brėžinio sudėtis aktyviame brėžinyje: mark, name, title1, title3
using TSD = Tekla.Structures.Drawing;
using TSM = Tekla.Structures.Model;
using T3D = Tekla.Structures.Geometry3d;
var log = new System.Text.StringBuilder();
var model = new TSM.Model();
var dh = new TSD.DrawingHandler();
var d = dh.GetActiveDrawing();
if (d == null || d.Mark != "{{mark}}") return "WRONG DRAWING";
// 1. savybės
d.Name = "{{name}}"; d.Title1 = "{{title1}}"; d.Title2 = ""; d.Title3 = "{{title3}}";
d.SetUserProperty("DR_DRAWN_BY", "Jokūbas Malinauskas"); d.SetUserProperty("comment", "C3");
d.Modify();
// 2. vaizdai: palikti didžiausią FrontView, kitus ištrinti
var sheet = d.GetSheet();
TSD.View main = null; int best = -1;
var views = new System.Collections.Generic.List<TSD.View>();
var ve = sheet.GetAllObjects(new[] { typeof(TSD.View) });
while (ve.MoveNext()) { var v = ve.Current as TSD.View; if (v != null) views.Add(v); }
foreach (var v in views) {
  if (v.ViewType != TSD.View.ViewTypes.FrontView) continue;
  int c = 0; var pe = v.GetAllObjects(new[] { typeof(TSD.Part) }); while (pe.MoveNext()) c++;
  if (c > best) { best = c; main = v; }
}
if (main == null) return "no front view";
foreach (var v in views) if (v != main) v.Delete();
main.Attributes.LoadAttributes("M.T assem.");
main.Attributes.Scale = 10;
main.Origin = new T3D.Point(150, 240, 0);
main.Modify();
// 3. dalių geometrija vaizdo koordinatėse
var wph = model.GetWorkPlaneHandler();
var savedPlane = wph.GetCurrentTransformationPlane();
System.Func<TSD.View, System.Collections.Generic.List<System.Tuple<TSM.Part, T3D.Point, T3D.Point>>> boxes = (vw) => {
  wph.SetCurrentTransformationPlane(savedPlane);
  vw.Select();
  wph.SetCurrentTransformationPlane(new TSM.TransformationPlane(vw.DisplayCoordinateSystem));
  var res = new System.Collections.Generic.List<System.Tuple<TSM.Part, T3D.Point, T3D.Point>>();
  var pe = vw.GetAllObjects(new[] { typeof(TSD.Part) });
  while (pe.MoveNext()) {
    var dp = pe.Current as TSD.Part; if (dp == null) continue;
    var mp = model.SelectModelObject(dp.ModelIdentifier) as TSM.Part; if (mp == null) continue;
    var s = mp.GetSolid();
    res.Add(System.Tuple.Create(mp, s.MinimumPoint, s.MaximumPoint));
  }
  return res;
};
var mb = boxes(main);
wph.SetCurrentTransformationPlane(savedPlane);
var asm = mb[0].Item1.GetAssembly();
var mainPart = asm.GetMainPart() as TSM.Part;
System.Tuple<TSM.Part, T3D.Point, T3D.Point> pipe = null, top = null, bas = null;
foreach (var b in mb) {
  if (b.Item1.Identifier.ID == mainPart.Identifier.ID) { pipe = b; continue; }
  if (top == null || b.Item2.X > top.Item2.X) top = b;
  if (bas == null || b.Item2.X < bas.Item2.X) bas = b;
}
if (pipe == null || top == null || bas == null || top == bas) return "unexpected parts: " + mb.Count;
log.Append("pipe " + pipe.Item1.Profile.ProfileString + " top " + top.Item1.Profile.ProfileString + " base " + bas.Item1.Profile.ProfileString + "; ");
// 4. pjūviai
var sva = new TSD.View.ViewAttributes("M.T pju. maž."); sva.Scale = 10;
var sma = new TSD.SectionMarkBase.SectionMarkAttributes("M.T. pjuvis");
double yr = System.Math.Max(System.Math.Abs(bas.Item2.Y), System.Math.Abs(bas.Item3.Y)) + 10;
double xa = top.Item2.X - 20, xb = bas.Item3.X + 20;
TSD.View va, vb; TSD.SectionMark ma, mbk;
TSD.View.CreateSectionView(main, new T3D.Point(xa, yr, 0), new T3D.Point(xa, -yr, 0), new T3D.Point(70, 240, 0), 40, 5, sva, sma, out va, out ma);
TSD.View.CreateSectionView(main, new T3D.Point(xb, -yr, 0), new T3D.Point(xb, yr, 0), new T3D.Point(70, 185, 0), 50, 5, sva, sma, out vb, out mbk);
if (va == null || vb == null) return log + "section failed";
va.Name = "A"; va.Modify(); vb.Name = "B"; vb.Modify();
if (ma != null) { ma.Attributes.MarkName = "A"; ma.Modify(); }
if (mbk != null) { mbk.Attributes.MarkName = "B"; mbk.Modify(); }
// 5. matmenys pjūviuose
var dsh = new TSD.StraightDimensionSetHandler();
var da = new TSD.StraightDimensionSet.StraightDimensionSetAttributes(null, "M.T matmenys");
da.Placing.Placing = TSD.DimensionSetBaseAttributes.Placings.Fixed;
System.Action<TSD.View, double[], double[], double, double, double> dim = (vw, xs, ys, ux, uy, paper) => {
  var pl = new TSD.PointList();
  for (int i = 0; i < xs.Length; i++) pl.Add(new T3D.Point(xs[i], ys[i], 0));
  dsh.CreateDimensionSet(vw, pl, new T3D.Vector(ux, uy, 0), paper * 10, da);
};
foreach (var sv in new[] { va, vb }) {
  var bx = boxes(sv);
  System.Tuple<TSM.Part, T3D.Point, T3D.Point> pp = null, pl8 = null;
  foreach (var b in bx) {
    if (b.Item1.Identifier.ID == mainPart.Identifier.ID) pp = b;
    else if (b.Item1.Identifier.ID == (sv == va ? top : bas).Item1.Identifier.ID) pl8 = b;
  }
  if (pp == null || pl8 == null) { log.Append(sv.Name + ": parts missing; "); continue; }
  double x0 = pl8.Item2.X, x1 = pl8.Item3.X, y0 = pl8.Item2.Y, y1 = pl8.Item3.Y;
  double px0 = pp.Item2.X, px1 = pp.Item3.X, py0 = pp.Item2.Y, py1 = pp.Item3.Y;
  var hx = new System.Collections.Generic.SortedSet<double>(); var hy = new System.Collections.Generic.SortedSet<double>();
  if (sv == va) {
    var bolts = pl8.Item1.GetBolts();
    while (bolts.MoveNext()) { var bg = bolts.Current as TSM.BoltGroup; if (bg == null) continue; bg.Select();
      foreach (T3D.Point p in bg.BoltPositions) { hx.Add(System.Math.Round(p.X, 1)); hy.Add(System.Math.Round(p.Y, 1)); } }
  }
  // taškai ant pačio profilio (liestinės taškai), ne ant plokštelės krašto
  double cx = (px0 + px1) / 2, cy = (py0 + py1) / 2;
  dim(sv, new[] { x0, px0, px1, x1 }, new[] { y0, cy, cy, y0 }, 0, -1, 10);
  dim(sv, new[] { x1, cx, cx, x1 }, new[] { y0, py0, py1, y1 }, 1, 0, 10);
  if (hx.Count > 0) {
    // taškai skylių centruose: viršutinė grandinė – viršutinė skylių eilė, kairė – kairysis stulpelis
    double hyTop = hy.Max, hxLeft = hx.Min;
    var xs = new System.Collections.Generic.List<double> { x0 }; xs.AddRange(hx); xs.Add(x1);
    var xsy = xs.Select(_ => hyTop).ToArray(); xsy[0] = y1; xsy[xsy.Length - 1] = y1;
    dim(sv, xs.ToArray(), xsy, 0, 1, 10);
    var ys = new System.Collections.Generic.List<double> { y0 }; ys.AddRange(hy); ys.Add(y1);
    var ysx = ys.Select(_ => hxLeft).ToArray(); ysx[0] = x0; ysx[ysx.Length - 1] = x0;
    dim(sv, ysx, ys.ToArray(), -1, 0, 10);
    log.Append(sv.Name + " holes " + hx.Count + "x" + hy.Count + "; ");
  } else {
    dim(sv, new[] { x0, x1 }, new[] { y0, y0 }, 0, -1, 20);
    dim(sv, new[] { x1, x1 }, new[] { y0, y1 }, 1, 0, 20);
  }
  log.Append(sv.Name + " plate " + System.Math.Round(x1 - x0) + "x" + System.Math.Round(y1 - y0) + "; ");
}
wph.SetCurrentTransformationPlane(savedPlane);
// 6. žymos pagrindiniame vaizde (Tekla 2025 kartais meta LeaderLinePlacing klaidą)
try {
int del = 0;
var me = main.GetAllObjects(new[] { typeof(TSD.Mark) });
var dl = new System.Collections.Generic.List<TSD.Mark>();
while (me.MoveNext()) { var m = me.Current as TSD.Mark; if (m == null) continue; var r = m.GetRelatedObjects();
  if (r.MoveNext() && (r.Current is TSD.Bolt || r.Current is TSD.Connection)) dl.Add(m); }
foreach (var m in dl) { m.Delete(); del++; }
log.Append("marks deleted " + del + "; ");
} catch (System.Exception ex) { log.Append("marks skipped: " + ex.GetType().Name + "; "); }
// 7. vamzdžio detalės vaizdas
TSD.Part tp = null;
var pe2 = main.GetAllObjects(new[] { typeof(TSD.Part) });
while (pe2.MoveNext()) { var p = pe2.Current as TSD.Part; if (p != null && p.ModelIdentifier.ID == mainPart.Identifier.ID) { tp = p; break; } }
d.CommitChanges();
var sel = dh.GetDrawingObjectSelector(); sel.UnselectAllObjects(); sel.SelectObject(tp);
Tekla.Structures.Model.Operations.Operation.RunMacro("..\\drawings\\ClaudeSinglePartView.cs");
var ve2 = d.GetSheet().GetAllObjects(new[] { typeof(TSD.View) });
while (ve2.MoveNext()) { var v = ve2.Current as TSD.View;
  if (v == null || v.ViewType != TSD.View.ViewTypes.FrontView) continue;
  int c = 0; var pe = v.GetAllObjects(new[] { typeof(TSD.Part) }); while (pe.MoveNext()) c++;
  if (c == 1) { v.Origin = new T3D.Point(150, 120, 0); v.Modify(); log.Append("detail ok; "); } }
d.CommitChanges();
log.ToString()
