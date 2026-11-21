// Brėžinio savybės: mark, name, title1, title3, drawnBy, comment (tik aktyviam brėžiniui su nurodytu mark)
using TSD = Tekla.Structures.Drawing;
var d = new TSD.DrawingHandler().GetActiveDrawing();
if (d == null || d.Mark != "{{mark}}") return "WRONG DRAWING: " + (d == null ? "none" : d.Mark);
d.Name = "{{name}}"; d.Title1 = "{{title1}}"; d.Title2 = ""; d.Title3 = "{{title3}}";
d.SetUserProperty("DR_DRAWN_BY", "{{drawnBy}}"); d.SetUserProperty("comment", "{{comment}}");
d.Modify(); d.CommitChanges();
d.Name + "|" + d.Title1 + "|" + d.Title3
