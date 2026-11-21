// Pagrindiniame vaizde (FrontView su daugiausia dalių) ištrina jungčių ir varžtų žymas
using TSD = Tekla.Structures.Drawing;
var d = new TSD.DrawingHandler().GetActiveDrawing();
if (d == null || d.Mark != "{{mark}}") return "WRONG DRAWING";
TSD.View main = null; int best = -1;
var ve = d.GetSheet().GetAllObjects(new[] { typeof(TSD.View) });
while (ve.MoveNext()) {
  var v = ve.Current as TSD.View;
  if (v == null || v.ViewType != TSD.View.ViewTypes.FrontView) continue;
  int c = 0; var pe = v.GetAllObjects(new[] { typeof(TSD.Part) }); while (pe.MoveNext()) c++;
  if (c > best) { best = c; main = v; }
}
if (main == null) return "no front view";
var del = new System.Collections.Generic.List<TSD.Mark>();
var me = main.GetAllObjects(new[] { typeof(TSD.Mark) });
while (me.MoveNext()) {
  var m = me.Current as TSD.Mark; if (m == null) continue;
  var rel = m.GetRelatedObjects();
  if (rel.MoveNext() && (rel.Current is TSD.Bolt || rel.Current is TSD.Connection)) del.Add(m);
}
foreach (var m in del) m.Delete();
d.CommitChanges();
"deleted " + del.Count
