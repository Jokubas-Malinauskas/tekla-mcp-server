// Surinkimo GUID pagal surinkimo poziciją (pos, pvz. IA-IV-5): grąžina guid ir pagrindinės dalies profilį
using TSM = Tekla.Structures.Model;
var model = new TSM.Model();
var en = model.GetModelObjectSelector().GetAllObjectsWithType(TSM.ModelObject.ModelObjectEnum.ASSEMBLY);
var res = new System.Collections.Generic.List<string>();
while (en.MoveNext()) {
  var a = en.Current as TSM.Assembly; if (a == null) continue;
  string pos = ""; a.GetReportProperty("ASSEMBLY_POS", ref pos);
  if (pos != "{{pos}}") continue;
  var mp = a.GetMainPart() as TSM.Part;
  res.Add(a.Identifier.GUID + " " + (mp == null ? "" : mp.Profile.ProfileString));
  if (res.Count >= 3) break;
}
string.Join(" | ", res)
