// Brėžinio užbaigimas (J.M). args: mark; scale=auto|10|20|keep; dimsclear=main,A (kur trinti visus tiesinius matmenis; "-" – niekur); layout=1|0
// Žymes tvarkyti PRIEŠ tai MCP įrankiais (list → delete → merge). 1) matmenų trynimas pagal dimsclear; 2) mastelis: pjūviai ir detalės 1:10, pagrindinis + A-A 1:10 jei telpa, kitaip 1:20; 3) išdėstymas: pagrindinis + A-A centre, kiti – kolonomis kairėje (x 28) ir dešinėje (x 255);
// 4) patikra: vaizdai už lapo, ant PASTABŲ, ant lentelės, persidengia.
using TSD = Tekla.Structures.Drawing;
using TSM = Tekla.Structures.Model;
var model = new TSM.Model();
var dh = new TSD.DrawingHandler();
var d = dh.GetActiveDrawing();
if (d == null || d.Mark != "{{mark}}") return "WRONG DRAWING";
var CI = System.Globalization.CultureInfo.InvariantCulture;
string scaleArg = "{{scale}}"; if (scaleArg == "" || scaleArg.StartsWith("{{")) scaleArg = "auto";
string dimsArg = "{{dimsclear}}"; if (dimsArg.StartsWith("{{")) dimsArg = "";
string layArg = "{{layout}}"; bool doLayout = !(layArg == "0");
var dimViews = new System.Collections.Generic.HashSet<string>(dimsArg.Split(',').Select(s => s.Trim()).Where(s => s != "" && s != "-"));
var rep = new System.Text.StringBuilder();

// --- vaizdai ---
System.Func<TSD.View, System.Collections.Generic.List<TSM.Part>> partsOf = (v) => {
  var l = new System.Collections.Generic.List<TSM.Part>();
  var pe = v.GetAllObjects(new[] { typeof(TSD.Part) });
  while (pe.MoveNext()) { var dp = pe.Current as TSD.Part; var mp = dp == null ? null : model.SelectModelObject(dp.ModelIdentifier) as TSM.Part; if (mp != null) l.Add(mp); }
  return l;
};
System.Func<TSM.Part, string> posOf = (p) => { string s = ""; p.GetReportProperty("PART_POS", ref s); return s; };
var all = new System.Collections.Generic.List<TSD.View>();
var ve = d.GetSheet().GetAllObjects(new[] { typeof(TSD.View) });
while (ve.MoveNext()) { var v = ve.Current as TSD.View; if (v != null) all.Add(v); }
var nP = new System.Collections.Generic.Dictionary<TSD.View, int>();
var detPos = new System.Collections.Generic.Dictionary<TSD.View, string>();
foreach (var v in all) { var ps = partsOf(v); nP[v] = ps.Count; if (ps.Count == 1) detPos[v] = posOf(ps[0]); }
TSD.View main = all.Where(v => v.ViewType != TSD.View.ViewTypes.SectionView).OrderByDescending(v => nP[v]).FirstOrDefault();
if (main == null || nP[main] < 2) return "no main view";
TSD.View va = all.FirstOrDefault(v => v.ViewType == TSD.View.ViewTypes.SectionView && v.Name == "A" && nP[v] > 1 && v.Width > main.Width * 0.6);
System.Func<TSD.View, string> key = (v) => v == main ? "main" : (v.ViewType == TSD.View.ViewTypes.SectionView ? v.Name : "D" + (detPos.ContainsKey(v) ? detPos[v] : "?"));

// --- 1) matmenys (žymės skripte neprieinamos: LeaderLinePlacing not serializable – jas tvarkyti MCP įrankiais) ---
int delDims = 0;
foreach (var v in all) {
  if (!dimViews.Contains(key(v))) continue;
  var del = new System.Collections.Generic.List<TSD.StraightDimensionSet>();
  var de = v.GetAllObjects(new[] { typeof(TSD.StraightDimensionSet) });
  while (de.MoveNext()) { var ds = de.Current as TSD.StraightDimensionSet; if (ds != null) del.Add(ds); }
  foreach (var ds in del) { ds.Delete(); delDims++; }
}
if (delDims > 0) { d.CommitChanges(); rep.Append("dims deleted " + delDims + "\n"); }

// --- 2) mastelis ---
System.Action<TSD.View, double> setScale = (v, s) => { if (System.Math.Abs(v.Attributes.Scale - s) > 0.01) { v.Attributes.Scale = s; v.Modify(); v.Select(); } };
if (scaleArg != "keep") {
  foreach (var v in all) if (v != main && v != va) setScale(v, 10);
  System.Func<bool> fits = () => main.Width <= 165 && main.Height + (va != null ? va.Height + 8 : 0) <= 240 && (va == null || va.Width <= 175);
  double want = scaleArg == "20" ? 20 : 10;
  setScale(main, want); if (va != null) setScale(va, want);
  if (scaleArg == "auto" && !fits()) { setScale(main, 20); if (va != null) setScale(va, 20); want = 20; }
  rep.Append("scale main 1:" + main.Attributes.Scale + (scaleArg == "auto" ? " (auto)" : "") + "\n");
  d.CommitChanges();
}

// --- 3) išdėstymas ---
System.Action<TSD.View, double, double> place = (v, minX, minY) => {
  v.Origin = new Tekla.Structures.Geometry3d.Point(minX - v.FrameOrigin.X, minY - v.FrameOrigin.Y, 0); v.Modify(); v.Select();
};
if (doLayout) {
  double colL = 28, topY = 287, gap = 6;
  double mx = System.Math.Max(95, 172 - main.Width / 2);
  place(main, mx, topY - main.Height);
  double mainLeft = mx;
  if (va != null) {
    // tas pats origin.x kaip pagrindinio – geometrija viena po kita
    double ox = main.Origin.X;
    va.Origin = new Tekla.Structures.Geometry3d.Point(ox, (main.Origin.Y + main.FrameOrigin.Y) - 8 - va.Height - va.FrameOrigin.Y, 0); va.Modify(); va.Select();
    mainLeft = System.Math.Min(mainLeft, ox + va.FrameOrigin.X);
  }
  var rest = all.Where(v => v != main && v != va).ToList();
  var secs = rest.Where(v => v.ViewType == TSD.View.ViewTypes.SectionView).OrderBy(v => v.Name).ToList();
  var dets = rest.Where(v => v.ViewType != TSD.View.ViewTypes.SectionView).OrderBy(v => key(v)).ToList();
  double rightX = System.Math.Max(255, mx + main.Width + 12);
  // kolonos: x, viršus, apačios riba
  var cx = new double[] { colL, rightX, 0 };
  var cy = new double[] { topY, topY, topY };
  var cBot = new double[] { 50, 130, 130 };
  var cW = new double[] { mainLeft - 6 - colL, 999, 999 };
  double rMaxW = 0;
  System.Func<TSD.View, int, bool> tryPut = (v, c) => {
    if (c == 2 && cx[2] == 0) return false;
    if (v.Width > cW[c] || cy[c] - v.Height < cBot[c]) return false;
    place(v, cx[c], cy[c] - v.Height); cy[c] -= v.Height + gap;
    if (c == 1) { rMaxW = System.Math.Max(rMaxW, v.Width); cx[2] = cx[1] + rMaxW + 10; }
    return true;
  };
  int i = 0; var unplaced = new System.Collections.Generic.List<string>();
  foreach (var v in secs) { int first = (i++ % 2 == 0) ? 0 : 1; if (!tryPut(v, first) && !tryPut(v, 1 - first) && !tryPut(v, 2)) unplaced.Add(key(v)); }
  foreach (var v in dets) {
    var order = new[] { 0, 1 }.OrderByDescending(c => cy[c] - cBot[c]).Concat(new[] { 2 }).ToArray();
    bool ok = false; foreach (var c in order) if (tryPut(v, c)) { ok = true; break; }
    if (!ok) unplaced.Add(key(v));
  }
  if (unplaced.Count > 0) rep.Append("NETILPO: " + string.Join(", ", unplaced) + "\n");
  d.CommitChanges();
}

// --- 4) patikra ---
var probs = new System.Collections.Generic.List<string>();
var boxes = all.Select(v => new { k = key(v), x0 = v.Origin.X + v.FrameOrigin.X, y0 = v.Origin.Y + v.FrameOrigin.Y, x1 = v.Origin.X + v.FrameOrigin.X + v.Width, y1 = v.Origin.Y + v.FrameOrigin.Y + v.Height, v = v }).ToList();
foreach (var b in boxes) {
  if (b.x0 < 8 || b.y0 < 8 || b.x1 > 412 || b.y1 > 290) probs.Add(b.k + " už lapo");
  if (b.x1 > 238 && b.y0 < 128) probs.Add(b.k + " ant PASTABŲ");
  if (b.x1 > 40 && b.x0 < 228 && b.y0 < 46) probs.Add(b.k + " ant lentelės");
}
for (int a = 0; a < boxes.Count; a++) for (int b = a + 1; b < boxes.Count; b++) {
  var p = boxes[a]; var q = boxes[b];
  if (p.x0 < q.x1 && q.x0 < p.x1 && p.y0 < q.y1 && q.y0 < p.y1) probs.Add(p.k + " persidengia su " + q.k);
}
rep.Append(probs.Count == 0 ? "patikra OK" : "PROBLEMOS: " + string.Join("; ", probs.Distinct()));
rep.ToString()
