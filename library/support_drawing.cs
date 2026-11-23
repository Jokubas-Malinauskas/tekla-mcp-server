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
var oldDims = new System.Collections.Generic.List<TSD.StraightDimensionSet>();
var de = main.GetAllObjects(new[] { typeof(TSD.StraightDimensionSet) });
while (de.MoveNext()) { var sd = de.Current as TSD.StraightDimensionSet; if (sd != null) oldDims.Add(sd); }
foreach (var sd in oldDims) sd.Delete();
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
System.Tuple<TSM.Part, T3D.Point, T3D.Point> pipe = mb.FirstOrDefault(b => b.Item1.Identifier.ID == mainPart.Identifier.ID), top = null, bas = null;
// padas ir viršutinė plokštelė – didžiausio tūrio plokštelės vamzdžio galuose; kitos (ausys) – papildomos
System.Func<System.Tuple<TSM.Part, T3D.Point, T3D.Point>, double> vol = (b) => (b.Item3.X - b.Item2.X) * (b.Item3.Y - b.Item2.Y) * (b.Item3.Z - b.Item2.Z);
if (pipe != null) {
  var others = mb.Where(b => b != pipe).ToList();
  top = others.Where(b => b.Item3.X > pipe.Item3.X - 2).OrderByDescending(vol).FirstOrDefault();
  bas = others.Where(b => b.Item2.X < pipe.Item2.X + 2 && b != top).OrderByDescending(vol).FirstOrDefault();
}
var ears = mb.Where(b => b != pipe && b != top && b != bas).ToList();
if (pipe == null || top == null || bas == null || top == bas) return "unexpected parts: " + mb.Count;
log.Append("pipe " + pipe.Item1.Profile.ProfileString + " top " + top.Item1.Profile.ProfileString + " base " + bas.Item1.Profile.ProfileString + " ears " + ears.Count + " | vols " + string.Join(",", mb.Where(b => b != pipe).Select(b => b.Item1.Profile.ProfileString + ":" + System.Math.Round(b.Item2.X) + ".." + System.Math.Round(b.Item3.X) + "/" + System.Math.Round(vol(b) / 1000))) + " pipe " + System.Math.Round(pipe.Item2.X) + ".." + System.Math.Round(pipe.Item3.X) + "; ");
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
  var pts0 = new System.Collections.Generic.List<T3D.Point>();
  for (int i = 0; i < xs.Length; i++) pts0.Add(new T3D.Point(xs[i], ys[i], 0));
  var pl = new TSD.PointList(); foreach (var p in pts0.OrderByDescending(p => p.X * ux + p.Y * uy)) pl.Add(p);
  dsh.CreateDimensionSet(vw, pl, new T3D.Vector(ux, uy, 0), paper * 10, da);
};
// gabaritai pagrindiniame vaizde: bendras ilgis apačioje, pado aukštis kairėje, viršutinės plokštelės dešinėje
var lowest = mb.OrderBy(b => b.Item2.X).First();
dim(main, new[] { lowest.Item2.X, top.Item3.X }, new[] { lowest.Item2.Y, top.Item2.Y }, 0, -1, 10);
dim(main, new[] { bas.Item2.X, bas.Item2.X }, new[] { bas.Item2.Y, bas.Item3.Y }, -1, 0, 10);
dim(main, new[] { top.Item3.X, top.Item3.X }, new[] { top.Item2.Y, top.Item3.Y }, 1, 0, 10);
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
  if (sv == vb && ears.Count > 0) {
    // ausų padėtis ant pado: pado kraštas → ausies kraštai → ... → pado kraštas (taškai ant ausų)
    var eb = bx.Where(b => ears.Any(e => e.Item1.Identifier.ID == b.Item1.Identifier.ID)).OrderBy(b => b.Item2.X).ToList();
    var ex = new System.Collections.Generic.List<double> { x0 }; var ey = new System.Collections.Generic.List<double> { y1 };
    foreach (var b in eb) { ex.Add(b.Item2.X); ey.Add(b.Item3.Y); ex.Add(b.Item3.X); ey.Add(b.Item3.Y); }
    ex.Add(x1); ey.Add(y1);
    dim(sv, ex.ToArray(), ey.ToArray(), 0, 1, 18);
    log.Append("ears dims " + eb.Count + "; ");
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
// 7. detalių vaizdai: vamzdis ir skirtingos ausys – vienu makroso paleidimu
d.CommitChanges();
var sel = dh.GetDrawingObjectSelector(); sel.UnselectAllObjects();
var want = new System.Collections.Generic.HashSet<int> { mainPart.Identifier.ID };
var donePos = new System.Collections.Generic.HashSet<string>();
foreach (var e in ears) { string pos = ""; e.Item1.GetReportProperty("PART_POS", ref pos); if (donePos.Add(pos)) want.Add(e.Item1.Identifier.ID); }
var pe2 = main.GetAllObjects(new[] { typeof(TSD.Part) });
while (pe2.MoveNext()) { var p = pe2.Current as TSD.Part; if (p != null && want.Contains(p.ModelIdentifier.ID)) sel.SelectObject(p); }
Tekla.Structures.Model.Operations.Operation.RunMacro("..\\drawings\\ClaudeSinglePartView.cs");
var all = new System.Collections.Generic.List<TSD.View>();
var v3 = d.GetSheet().GetAllObjects(new[] { typeof(TSD.View) });
while (v3.MoveNext()) { var v = v3.Current as TSD.View; if (v != null) all.Add(v); }
System.Func<TSD.View, int> nParts = (v) => { int c = 0; var pe = v.GetAllObjects(new[] { typeof(TSD.Part) }); while (pe.MoveNext()) c++; return c; };
foreach (var v in all.Where(v => v.ViewType != TSD.View.ViewTypes.FrontView && v.ViewType != TSD.View.ViewTypes.SectionView && nParts(v) == 1).ToList()) { v.Delete(); all.Remove(v); }
System.Func<double, double, T3D.Point> P = (x, y) => new T3D.Point(x, y, 0);
System.Action<TSD.View, System.Collections.Generic.List<T3D.Point>, double, double, double> dimP = (vw, pts, ux, uy, paper) => {
  var pl = new TSD.PointList(); foreach (var p in pts.OrderByDescending(p => p.X * ux + p.Y * uy)) pl.Add(p);
  dsh.CreateDimensionSet(vw, pl, new T3D.Vector(ux, uy, 0), paper * vw.Attributes.Scale, da);
};
var details = all.Where(v => v.ViewType == TSD.View.ViewTypes.FrontView && nParts(v) == 1).ToList();
// detalių matmenys pagal taisykles: plokštelė su skylėmis – viršuje ir kairėje skylių grandinės, apačioje plotis, dešinėje aukštis;
// plokštelė be skylių – nuosklembų pusėje grandinė (15 102 15), kitoje bendras; šonuose 60 15 ir 75; profiliams – tik dublių šalinimas
foreach (var v in details) {
  wph.SetCurrentTransformationPlane(savedPlane); v.Select();
  wph.SetCurrentTransformationPlane(new TSM.TransformationPlane(v.DisplayCoordinateSystem));
  TSM.Part mp = null; var pe = v.GetAllObjects(new[] { typeof(TSD.Part) });
  while (pe.MoveNext()) { var dp = pe.Current as TSD.Part; if (dp != null) mp = model.SelectModelObject(dp.ModelIdentifier) as TSM.Part; }
  if (mp == null) continue;
  var sol = mp.GetSolid(); double x0 = sol.MinimumPoint.X, x1 = sol.MaximumPoint.X, y0 = sol.MinimumPoint.Y, y1 = sol.MaximumPoint.Y;
  var dsets = new System.Collections.Generic.List<TSD.StraightDimensionSet>();
  var de2 = v.GetAllObjects(new[] { typeof(TSD.StraightDimensionSet) });
  while (de2.MoveNext()) { var sd = de2.Current as TSD.StraightDimensionSet; if (sd != null) dsets.Add(sd); }
  bool isPlate = mp.Profile.ProfileString.StartsWith("PL");
  if (!isPlate) {
    // profilis: ištrinti viršutinį bendrą ilgį ir kairįjį bendrą aukštį (dubliai)
    foreach (var sd in dsets) {
      var ce = sd.GetObjects(); TSD.StraightDimension one = null; int n = 0;
      while (ce.MoveNext()) if (ce.Current is TSD.StraightDimension s1) { one = s1; n++; }
      if (n != 1 || one == null) continue;
      double ddx = System.Math.Abs(one.EndPoint.X - one.StartPoint.X), ddy = System.Math.Abs(one.EndPoint.Y - one.StartPoint.Y);
      var up = one.UpDirection;
      if (up.Y > 0.5 && ddx > (x1 - x0) - 2) sd.Delete();
      else if (up.X < -0.5 && ddy > (y1 - y0) - 2) sd.Delete();
    }
    // Jokūbas: jei galas nupjautas įstrižai – kampo matmuo (pvz., 87°) tarp profilio krašto ir nuopjovos
    var pv2 = new System.Collections.Generic.List<T3D.Point>();
    var fe2 = sol.GetFaceEnumerator();
    while (fe2.MoveNext()) { var f = fe2.Current as Tekla.Structures.Solid.Face; if (f == null) continue; var le = f.GetLoopEnumerator();
      while (le.MoveNext()) { var lp = le.Current as Tekla.Structures.Solid.Loop; if (lp == null) continue; var pv = lp.GetVertexEnumerator(); while (pv.MoveNext()) { var pt = pv.Current as T3D.Point; if (pt != null) pv2.Add(pt); } } }
    foreach (bool startEnd in new[] { true, false }) {
      var endPts = pv2.Where(p => startEnd ? p.X < x0 + (x1 - x0) / 4 : p.X > x1 - (x1 - x0) / 4).ToList();
      var tPt = endPts.Where(p => System.Math.Abs(p.Y - y1) < 0.6).OrderBy(p => startEnd ? p.X : -p.X).FirstOrDefault();
      var bPt = endPts.Where(p => System.Math.Abs(p.Y - y0) < 0.6).OrderBy(p => startEnd ? p.X : -p.X).FirstOrDefault();
      if (tPt == null || bPt == null || System.Math.Abs(tPt.X - bPt.X) < 0.5) continue;
      bool topOut = startEnd ? tPt.X < bPt.X : tPt.X > bPt.X;
      var o = topOut ? tPt : bPt; var other = topOut ? bPt : tPt;
      var along = new T3D.Point(startEnd ? x1 : x0, o.Y, 0);
      var ad = new TSD.AngleDimension(v, new T3D.Point(o.X, o.Y, 0), along, new T3D.Point(other.X, other.Y, 0), 12, new TSD.AngleDimensionAttributes("M.T laipsniai"));
      if (ad.Insert()) log.Append("angle; ");
    }
    continue;
  }
  foreach (var sd in dsets) sd.Delete();
  // kontūro taškai ant gabarito kraštų (skylių taškai į kraštus nepatenka)
  var vx = new System.Collections.Generic.List<T3D.Point>();
  var fe = sol.GetFaceEnumerator();
  while (fe.MoveNext()) { var f = fe.Current as Tekla.Structures.Solid.Face; if (f == null) continue;
    var le = f.GetLoopEnumerator();
    while (le.MoveNext()) { var lp = le.Current as Tekla.Structures.Solid.Loop; if (lp == null) continue;
      var pv = lp.GetVertexEnumerator(); while (pv.MoveNext()) { var pt = pv.Current as T3D.Point; if (pt != null) vx.Add(pt); } } }
  double e = 0.5;
  System.Func<System.Func<T3D.Point, bool>, System.Collections.Generic.List<T3D.Point>> on = (f) => vx.Where(f).ToList();
  var bot = on(p => System.Math.Abs(p.Y - y0) < e); var tp = on(p => System.Math.Abs(p.Y - y1) < e);
  var lft = on(p => System.Math.Abs(p.X - x0) < e); var rgt = on(p => System.Math.Abs(p.X - x1) < e);
  double xbMin = bot.Count > 0 ? bot.Min(p => p.X) : x0, xbMax = bot.Count > 0 ? bot.Max(p => p.X) : x1;
  double xtMin = tp.Count > 0 ? tp.Min(p => p.X) : x0, xtMax = tp.Count > 0 ? tp.Max(p => p.X) : x1;
  double ylMin = lft.Count > 0 ? lft.Min(p => p.Y) : y0, ylMax = lft.Count > 0 ? lft.Max(p => p.Y) : y1;
  double yrMin = rgt.Count > 0 ? rgt.Min(p => p.Y) : y0, yrMax = rgt.Count > 0 ? rgt.Max(p => p.Y) : y1;
  var hx = new System.Collections.Generic.SortedSet<double>(); var hy = new System.Collections.Generic.SortedSet<double>();
  var be = mp.GetBolts();
  while (be.MoveNext()) { var bg = be.Current as TSM.BoltGroup; if (bg == null) continue; bg.Select();
    foreach (T3D.Point p in bg.BoltPositions) { hx.Add(System.Math.Round(p.X, 1)); hy.Add(System.Math.Round(p.Y, 1)); } }
  System.Func<System.Collections.Generic.IEnumerable<T3D.Point>, bool, System.Collections.Generic.List<T3D.Point>> uniq = (pts, byX) =>
    pts.GroupBy(p => System.Math.Round(byX ? p.X : p.Y, 1)).Select(g => g.First()).ToList();
  double dd = 7;
  if (hx.Count > 0) {
    double hyTop = hy.Max, hxLeft = hx.Min;
    var t = new System.Collections.Generic.List<T3D.Point> { P(x0, y1) }; foreach (var x in hx) t.Add(P(x, hyTop)); t.Add(P(x1, y1));
    dimP(v, t, 0, 1, dd);
    var l = new System.Collections.Generic.List<T3D.Point> { P(x0, y0) }; foreach (var y in hy) l.Add(P(hxLeft, y)); l.Add(P(x0, y1));
    dimP(v, l, -1, 0, dd);
    dimP(v, new System.Collections.Generic.List<T3D.Point> { P(x0, y0), P(x1, y0) }, 0, -1, dd);
    dimP(v, new System.Collections.Generic.List<T3D.Point> { P(x1, y0), P(x1, y1) }, 1, 0, dd);
  } else {
    dimP(v, uniq(new[] { P(x0, ylMin), P(xbMin, y0), P(xbMax, y0), P(x1, yrMin) }, true), 0, -1, dd);
    dimP(v, uniq(new[] { P(x0, ylMax), P(xtMin, y1), P(xtMax, y1), P(x1, yrMax) }, true), 0, 1, dd);
    dimP(v, uniq(new[] { P(xtMin, y1), P(x0, ylMax), P(x0, ylMin), P(xbMin, y0) }, false), -1, 0, dd);
    dimP(v, new System.Collections.Generic.List<T3D.Point> { P(xtMax, y1), P(xbMax, y0) }, 1, 0, dd);
  }
}
wph.SetCurrentTransformationPlane(savedPlane);
// išdėstymas: pagrindinis centre viršuje, pjūviai kairėje kolonoje, detalės antroje kolonoje; netilpę – eilėje po pagrindiniu (PASTABŲ nelieti)
System.Action<TSD.View, double, double> place = (v, cx, cy) => {
  v.Select();
  v.Origin = new T3D.Point(cx - v.FrameOrigin.X - v.Width / 2, cy - v.FrameOrigin.Y - v.Height / 2, 0);
  v.Modify(); };
var mainV = all.FirstOrDefault(v => v.ViewType == TSD.View.ViewTypes.FrontView && nParts(v) > 1);
double gap = 6;
var secV = all.Where(v => v.ViewType == TSD.View.ViewTypes.SectionView).OrderBy(v => v.Name).ToList();
foreach (var v in secV) v.Select(); foreach (var v in details) v.Select();
// 1 kolona – pjūviai, 2 kolona – detalės; kolonų plotis pagal plačiausią vaizdą; pagrindinis dešiniau
double c1w = secV.Count > 0 ? secV.Max(v => v.Width) : 0, c2x = 14 + c1w + gap;
double c2w = details.Count > 0 ? details.Max(v => v.Width) : 0, colRight = c2x + c2w + gap;
if (mainV != null) { mainV.Select(); place(mainV, System.Math.Max(200, colRight + 4 + mainV.Width / 2), 287 - 10 - mainV.Height / 2); }
var rest = new System.Collections.Generic.List<TSD.View>();
double y1 = 287, y2 = 287;
foreach (var v in secV) { if (y1 - v.Height >= 100) { place(v, 14 + v.Width / 2, y1 - v.Height / 2); y1 -= v.Height + gap; } else rest.Add(v); }
foreach (var v in details) { if (y2 - v.Height >= 100) { place(v, c2x + v.Width / 2, y2 - v.Height / 2); y2 -= v.Height + gap; } else rest.Add(v); }
double yRow = 287; if (mainV != null) { mainV.Select(); yRow = mainV.Origin.Y + mainV.FrameOrigin.Y - gap; }
double rx2 = colRight, rh = 0;
foreach (var v in rest) {
  v.Select();
  double xmax = (yRow - v.Height < 125) ? 238 : 412;
  if (rx2 + v.Width > xmax) { yRow -= rh + gap + 8; rx2 = colRight; rh = 0; xmax = (yRow - v.Height < 125) ? 238 : 412; }
  place(v, rx2 + v.Width / 2, yRow - v.Height / 2); rx2 += v.Width + gap; rh = System.Math.Max(rh, v.Height);
}
log.Append("details " + details.Count + "; ");
d.CommitChanges();
log.ToString()
