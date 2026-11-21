// Detalės vaizdas (makrosas ClaudeSinglePartView) daliai guid iš pagrindinio FrontView; mark – apsauga
using TSD = Tekla.Structures.Drawing;
var model = new Model();
var dh = new TSD.DrawingHandler();
var d = dh.GetActiveDrawing();
if (d == null || d.Mark != "{{mark}}") return "WRONG DRAWING";
var id = model.GetIdentifierByGUID("{{guid}}");
TSD.Part target = null;
var ve = d.GetSheet().GetAllObjects(new[] { typeof(TSD.View) });
while (ve.MoveNext() && target == null) {
  var v = ve.Current as TSD.View;
  if (v == null || v.ViewType != TSD.View.ViewTypes.FrontView) continue;
  var pe = v.GetAllObjects(new[] { typeof(TSD.Part) });
  while (pe.MoveNext()) { var p = pe.Current as TSD.Part; if (p != null && p.ModelIdentifier.ID == id.ID) { target = p; break; } }
}
if (target == null) return "part not found";
var sel = dh.GetDrawingObjectSelector();
sel.UnselectAllObjects(); sel.SelectObject(target);
"macro " + Tekla.Structures.Model.Operations.Operation.RunMacro("..\\drawings\\ClaudeSinglePartView.cs")
