// A tipo sijos brėžinys aktyviame brėžinyje: mark, name, title1, title3 (pagrindinis 1:20, A-A iš viršaus, B-B prie tarpinės plokštelės, C-C prie galinės, detalės)
using TSD = Tekla.Structures.Drawing;
using TSM = Tekla.Structures.Model;
using T3D = Tekla.Structures.Geometry3d;
var log = new System.Text.StringBuilder();
var model = new TSM.Model();
var dh = new TSD.DrawingHandler();
var d = dh.GetActiveDrawing();
if (d == null || d.Mark != "{{mark}}") return "WRONG DRAWING";
d.Name = "{{name}}"; d.Title1 = "{{title1}}"; d.Title2 = ""; d.Title3 = "{{title3}}";
d.SetUserProperty("DR_DRAWN_BY", "Jokūbas Malinauskas"); d.SetUserProperty("comment", "C3");
d.Modify();
// vaizdai
var views = new System.Collections.Generic.List<TSD.View>();
var ve = d.GetSheet().GetAllObjects(new[] { typeof(TSD.View) });
while (ve.MoveNext()) { var v = ve.Current as TSD.View; if (v != null) views.Add(v); }
TSD.View main = null; int best = -1;
foreach (var v in views) {
  if (v.ViewType != TSD.View.ViewTypes.FrontView) continue;
  int c = 0; var pe = v.GetAllObjects(new[] { typeof(TSD.Part) }); while (pe.MoveNext()) c++;
  if (c > best) { best = c; main = v; }
}
if (main == null) return "no front view";
foreach (var v in views) if (v != main) v.Delete();
main.Attributes.LoadAttributes("M.T assem.");
main.Attributes.Scale = 20;
main.Origin = new T3D.Point(210, 235, 0);
main.Modify();
var oldDims = new System.Collections.Generic.List<TSD.StraightDimensionSet>();
var de = main.GetAllObjects(new[] { typeof(TSD.StraightDimensionSet) });
while (de.MoveNext()) { var sd = de.Current as TSD.StraightDimensionSet; if (sd != null) oldDims.Add(sd); }
foreach (var sd in oldDims) sd.Delete();
var wph = model.GetWorkPlaneHandler();
var gp = wph.GetCurrentTransformationPlane();
System.Func<TSD.View, System.Collections.Generic.List<System.Tuple<TSM.Part, T3D.Point, T3D.Point>>> boxes = (vw) => {
  wph.SetCurrentTransformationPlane(gp); vw.Select();
  wph.SetCurrentTransformationPlane(new TSM.TransformationPlane(vw.DisplayCoordinateSystem));
  var res = new System.Collections.Generic.List<System.Tuple<TSM.Part, T3D.Point, T3D.Point>>();
  var pe = vw.GetAllObjects(new[] { typeof(TSD.Part) });
  while (pe.MoveNext()) {
    var dp = pe.Current as TSD.Part; if (dp == null) continue;
    var mp = model.SelectModelObject(dp.ModelIdentifier) as TSM.Part; if (mp == null) continue;
    var s = mp.GetSolid(); res.Add(System.Tuple.Create(mp, s.MinimumPoint, s.MaximumPoint));
  }
  return res;
};
System.Func<TSM.Part, bool> hasBolts = (p) => { var e = p.GetBolts(); return e.MoveNext(); };
System.Func<System.Collections.Generic.List<System.Tuple<TSM.Part, T3D.Point, T3D.Point>>, int, System.Tuple<TSM.Part, T3D.Point, T3D.Point>> byId = (l, id) => l.FirstOrDefault(b => b.Item1.Identifier.ID == id);
var mb = boxes(main);
var mainPart = mb[0].Item1.GetAssembly().GetMainPart() as TSM.Part;
var beam = byId(mb, mainPart.Identifier.ID);
var sec = mb.Where(b => b.Item1.Identifier.ID != mainPart.Identifier.ID).ToList();
double bx0 = beam.Item2.X, bx1 = beam.Item3.X;
var endR = sec.Where(b => hasBolts(b.Item1) && b.Item3.X >= bx1 - 15).OrderByDescending(b => b.Item3.X).FirstOrDefault();
var endL = sec.Where(b => hasBolts(b.Item1) && b.Item2.X <= bx0 + 15).OrderBy(b => b.Item2.X).FirstOrDefault();
var mids = sec.Where(b => hasBolts(b.Item1) && b != endR && b != endL).OrderBy(b => b.Item2.X).ToList();
log.Append(mainPart.Profile.ProfileString + " L" + System.Math.Round(bx1 - bx0) + ", secondaries " + sec.Count + ", mid plates " + mids.Count + "; ");
// matmenys
var dsh = new TSD.StraightDimensionSetHandler();
var da = new TSD.StraightDimensionSet.StraightDimensionSetAttributes(null, "M.T matmenys");
da.Placing.Placing = TSD.DimensionSetBaseAttributes.Placings.Fixed;
System.Action<TSD.View, System.Collections.Generic.List<T3D.Point>, double, double, double> dim = (vw, pts, ux, uy, paper) => {
  var pl = new TSD.PointList(); foreach (var p in pts) pl.Add(p);
  dsh.CreateDimensionSet(vw, pl, new T3D.Vector(ux, uy, 0), paper * vw.Attributes.Scale, da);
};
System.Func<double, double, T3D.Point> P = (x, y) => new T3D.Point(x, y, 0);
// pagrindinis: apačioje bendras ilgis, viršuje tarpinių plokštelių grandinė, šonuose galinių plokštelių iškyša
double ax0 = mb.Min(b => b.Item2.X), ax1 = mb.Max(b => b.Item3.X), by0 = beam.Item2.Y, by1 = beam.Item3.Y;
var left = mb.OrderBy(b => b.Item2.X).First(); var right = mb.OrderByDescending(b => b.Item3.X).First();
dim(main, new System.Collections.Generic.List<T3D.Point> { P(ax0, left.Item2.Y), P(ax1, right.Item2.Y) }, 0, -1, 15);
if (mids.Count > 0) {
  var top = new System.Collections.Generic.List<T3D.Point> { P(ax0, left.Item3.Y) };
  foreach (var m in mids) { top.Add(P(m.Item2.X, m.Item3.Y)); top.Add(P(m.Item3.X, m.Item3.Y)); }
  top.Add(P(ax1, right.Item3.Y));
  dim(main, top, 0, 1, 10);
}
foreach (var ep in new[] { endR, endL }) {
  if (ep == null) continue;
  bool r = ep == endR; double ex = r ? ep.Item3.X : ep.Item2.X; double bx = r ? bx1 : bx0;
  dim(main, new System.Collections.Generic.List<T3D.Point> { P(ex, ep.Item2.Y), P(bx, by0), P(bx, by1), P(ex, ep.Item3.Y) }, r ? 1 : -1, 0, 10);
}
// pjūviai
var sva = new TSD.View.ViewAttributes("M.T pju. maž.");
var sma = new TSD.SectionMarkBase.SectionMarkAttributes("M.T. pjuvis");
double yTop = mb.Max(b => b.Item3.Y) + 15, yBot = mb.Min(b => b.Item2.Y) - 15;
TSD.View va; TSD.SectionMark ma;
sva.Scale = 20;
// A-A: pjūvis virš profilio, iš dešinės į kairę -> žiūri žemyn
TSD.View.CreateSectionView(main, P(ax1 + 20, yTop), P(ax0 - 20, yTop), new T3D.Point(210, 165, 0), by1 - by0 + 60, by1 - by0 + 60, sva, sma, out va, out ma);
if (va != null) {
  va.Name = "A"; va.Modify(); va.Select(); main.Select();
  va.Attributes.Shortening = main.Attributes.Shortening; va.Modify();
  if (ma != null) { ma.Attributes.MarkName = "A"; ma.Modify(); }
  va.Origin = new T3D.Point(210, 165, 0); va.Modify();
  var ab = boxes(va); var abeam = byId(ab, mainPart.Identifier.ID);
  var xs = new System.Collections.Generic.List<T3D.Point> { P(ab.Min(b => b.Item2.X), abeam.Item3.Y) };
  foreach (var s in ab.Where(b => b.Item1.Identifier.ID != mainPart.Identifier.ID).OrderBy(b => b.Item2.X)) {
    if (s.Item2.X > xs[0].X + 1) xs.Add(P(s.Item2.X, s.Item3.Y));
    if (s.Item3.X < ab.Max(b => b.Item3.X) - 1) xs.Add(P(s.Item3.X, s.Item3.Y));
  }
  xs.Add(P(ab.Max(b => b.Item3.X), abeam.Item3.Y));
  dim(va, xs, 0, 1, 10);
  double rx = abeam.Item3.X;
  dim(va, new System.Collections.Generic.List<T3D.Point> { P(rx, abeam.Item2.Y), P(rx, abeam.Item3.Y) }, 1, 0, 10);
  log.Append("A ok; ");
}
sva.Scale = 10;
var secs = new System.Collections.Generic.List<System.Tuple<string, System.Tuple<TSM.Part, T3D.Point, T3D.Point>, T3D.Point>>();
if (mids.Count > 0) secs.Add(System.Tuple.Create("B", mids[0], new T3D.Point(60, 95, 0)));
if (endR != null) secs.Add(System.Tuple.Create(mids.Count > 0 ? "C" : "B", endR, new T3D.Point(125, 95, 0)));
foreach (var sdef in secs) {
  var pb = sdef.Item2; double cutX = pb.Item2.X - 30;
  TSD.View sv; TSD.SectionMark sm;
  // iš viršaus žemyn -> žiūri į +x (į plokštelę)
  TSD.View.CreateSectionView(main, P(cutX, yTop), P(cutX, yBot), sdef.Item3, 5, pb.Item3.X - cutX + 5, sva, sma, out sv, out sm);
  if (sv == null) { log.Append(sdef.Item1 + " failed; "); continue; }
  sv.Name = sdef.Item1; sv.Modify(); if (sm != null) { sm.Attributes.MarkName = sdef.Item1; sm.Modify(); }
  var bx = boxes(sv); var pl8 = byId(bx, pb.Item1.Identifier.ID); var bm = byId(bx, mainPart.Identifier.ID);
  if (pl8 == null || bm == null) { log.Append(sdef.Item1 + " parts missing; "); continue; }
  double x0 = pl8.Item2.X, x1 = pl8.Item3.X, y0 = pl8.Item2.Y, y1 = pl8.Item3.Y;
  double cx = (bm.Item2.X + bm.Item3.X) / 2;
  var hx = new System.Collections.Generic.SortedSet<double>(); var hy = new System.Collections.Generic.SortedSet<double>();
  var be = pl8.Item1.GetBolts();
  while (be.MoveNext()) { var bg = be.Current as TSM.BoltGroup; if (bg == null) continue; bg.Select();
    foreach (T3D.Point p in bg.BoltPositions) { hx.Add(System.Math.Round(p.X, 1)); hy.Add(System.Math.Round(p.Y, 1)); } }
  // plokštelės iškyša virš/po profiliu: taškai ant profilio
  dim(sv, new System.Collections.Generic.List<T3D.Point> { P(x1, y0), P(cx, bm.Item2.Y), P(cx, bm.Item3.Y), P(x1, y1) }, 1, 0, 10);
  if (hx.Count > 0) {
    double hyTop = hy.Max, hxLeft = hx.Min;
    var t = new System.Collections.Generic.List<T3D.Point> { P(x0, y1) }; foreach (var x in hx) t.Add(P(x, hyTop)); t.Add(P(x1, y1));
    dim(sv, t, 0, 1, 10);
    var l = new System.Collections.Generic.List<T3D.Point> { P(x0, y0) }; foreach (var y in hy) l.Add(P(hxLeft, y)); l.Add(P(x0, y1));
    dim(sv, l, -1, 0, 10);
  }
  dim(sv, new System.Collections.Generic.List<T3D.Point> { P(x0, y0), P(x1, y0) }, 0, -1, 10);
  log.Append(sdef.Item1 + " plate " + System.Math.Round(x1 - x0) + "x" + System.Math.Round(y1 - y0) + " holes " + hx.Count + "x" + hy.Count + "; ");
}
wph.SetCurrentTransformationPlane(gp);
d.CommitChanges();
// detalių vaizdai: po vieną kiekvienai antrinei pozicijai
var done = new System.Collections.Generic.HashSet<string>();
var sel = dh.GetDrawingObjectSelector();
double dx = 200;
foreach (var s in sec) {
  string pos = ""; s.Item1.GetReportProperty("PART_POS", ref pos);
  if (!done.Add(pos)) continue;
  TSD.Part tp = null; var pe = main.GetAllObjects(new[] { typeof(TSD.Part) });
  while (pe.MoveNext()) { var p = pe.Current as TSD.Part; if (p != null && p.ModelIdentifier.ID == s.Item1.Identifier.ID) { tp = p; break; } }
  if (tp == null) continue;
  var before = new System.Collections.Generic.HashSet<int>();
  var v0 = d.GetSheet().GetAllObjects(new[] { typeof(TSD.View) }); while (v0.MoveNext()) before.Add(v0.Current.GetHashCode());
  sel.UnselectAllObjects(); sel.SelectObject(tp);
  Tekla.Structures.Model.Operations.Operation.RunMacro("..\\drawings\\ClaudeSinglePartView.cs");
  log.Append("detail " + pos + "; ");
}
// galutinis išdėstymas pagal rėmelio centrą (Tekla perdėlioja vaizdus juos kuriant)
System.Action<TSD.View, double, double> place = (v, cx, cy) => {
  v.Select();
  v.Origin = new T3D.Point(cx - v.FrameOrigin.X - v.Width / 2, cy - v.FrameOrigin.Y - v.Height / 2, 0);
  v.Modify(); };
place(main, 200, 240);
if (va != null) place(va, 200, 180);
var all = new System.Collections.Generic.List<TSD.View>();
var v3 = d.GetSheet().GetAllObjects(new[] { typeof(TSD.View) });
while (v3.MoveNext()) { var v = v3.Current as TSD.View; if (v != null) all.Add(v); }
double sx = 55;
foreach (var v in all.Where(v => v.ViewType == TSD.View.ViewTypes.SectionView && v.Name != "A").OrderBy(v => v.Name)) { place(v, sx, 100); sx += 60; }
System.Func<TSD.View, int> nParts = (v) => { int c = 0; var pe = v.GetAllObjects(new[] { typeof(TSD.Part) }); while (pe.MoveNext()) c++; return c; };
foreach (var v in all.Where(v => v.ViewType == TSD.View.ViewTypes.FrontView)) { if (nParts(v) > 1) place(v, 200, 240); else { place(v, sx, 100); sx += 65; } }
d.CommitChanges();
log.ToString()
