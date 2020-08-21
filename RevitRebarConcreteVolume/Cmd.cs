using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitRebarConcreteVolume
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    class Cmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
			string rebarVolumeParamName = "Арм.ОбъемБетона";
			Document doc = commandData.Application.ActiveUIDocument.Document;

			List<BuiltInCategory> constrCats = new List<BuiltInCategory> {
				BuiltInCategory.OST_StructuralFraming,
				BuiltInCategory.OST_StructuralColumns,
				BuiltInCategory.OST_Floors,
				BuiltInCategory.OST_Walls,
				BuiltInCategory.OST_StructuralFoundation
			};
			List<Element> constrs = new FilteredElementCollector(doc)
				.WherePasses(new ElementMulticategoryFilter(constrCats))
				.ToElements()
				.ToList();


			List<Element> rebars = new FilteredElementCollector(doc)
				.OfCategory(BuiltInCategory.OST_Rebar)
				.ToElements()
				.ToList();

			Dictionary<string, double> constrsDB = new Dictionary<string, double>();

			foreach (Element constr in constrs)
			{
				Parameter markParam = constr.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
				if (markParam == null) continue;
				if (!markParam.HasValue) continue;
				string mark = markParam.AsString();
				if (!mark.Contains("-")) continue;
				string[] ms = mark.Split('-');
				string prefix = ms[0] + "-" + ms[1];

				Parameter volumeParam = constr.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED);
				if (volumeParam == null) continue;
				if (!volumeParam.HasValue) continue;

				double volume = volumeParam.AsDouble();

				if (constrsDB.ContainsKey(prefix))
				{
					constrsDB[prefix] += volume;
				}
				else
				{
					constrsDB.Add(prefix, volume);
				}
			}

			using (Transaction t = new Transaction(doc))
			{
				t.Start("Зпись объемов бетона в арматуру");

				foreach (Element rebar in rebars)
				{

					Parameter isIfcRebarParam = rebar.LookupParameter("Арм.ВыполненаСемейством");
					if (isIfcRebarParam == null)
					{
						ElementId typeId = rebar.GetTypeId();
						if (typeId == ElementId.InvalidElementId) continue;
						Element typeElem = doc.GetElement(typeId);
						if (typeElem == null) continue;
						isIfcRebarParam = typeElem.LookupParameter("Арм.ВыполненаСемейством");
						if (isIfcRebarParam == null) continue;
					}
					int isIfc = isIfcRebarParam.AsInteger();
					Parameter hostNameParam = null;
					if (isIfc == 1)
					{
						hostNameParam = rebar.LookupParameter("Мрк.МаркаКонструкции");
					}
					else
					{
						hostNameParam = rebar.get_Parameter(BuiltInParameter.REBAR_ELEM_HOST_MARK);
					}
					if (hostNameParam == null) continue;
					if (!hostNameParam.HasValue) continue;

					string hostMark = hostNameParam.AsString();
					if (!hostMark.Contains("-")) continue;
					string[] ms = hostMark.Split('-');
					string prefix = ms[0] + "-" + ms[1];

					if (!constrsDB.ContainsKey(prefix))
					{
						TaskDialog.Show("Ошибка", "У арматуры есть марка, а такой конструкции нет: " + prefix);
						return Result.Failed;
					}

					double volume = constrsDB[prefix];

					Parameter volumeParam = rebar.LookupParameter(rebarVolumeParamName);
					if (volumeParam == null)
					{
						TaskDialog.Show("Ошибка", "Нет параметра " + rebarVolumeParamName);
						return Result.Failed;
					}
					volumeParam.Set(volume);
				}

				t.Commit();
			}

			return Result.Succeeded;
        }
    }
}
