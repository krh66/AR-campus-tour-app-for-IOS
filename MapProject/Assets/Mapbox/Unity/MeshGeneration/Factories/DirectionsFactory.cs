namespace Mapbox.Unity.MeshGeneration.Factories
{
	using UnityEngine;
	using Mapbox.Directions;
	using System.Collections.Generic;
	using System.Linq;
	using Mapbox.Unity.Map;
	using Data;
	using Modifiers;
	using Mapbox.Utils;
	using Mapbox.Unity.Utilities;
	using System.Collections;
    using System;
    using UnityEngine.UI;

    public class DirectionsFactory : MonoBehaviour
	{
		[SerializeField]
		AbstractMap _map;

		[SerializeField]
		MeshModifier[] MeshModifiers;
		[SerializeField]
		Material _material;

		[SerializeField]
		public Transform[] _waypoints;
		private List<Vector3> _cachedWaypoints;
		public List<Vector3> _waypointsOnMap;

		[SerializeField]
		[Range(1, 10)]
		private float UpdateFrequency = 2;

		public Vector2d[] _waypointsGeo;
		public List<Vector2d> _arMark;
		private int _wayCounter = 0;
		private Directions _directions;
		private int _counter;
		public Boolean scheduleOrNot;
		public Vector3 _userPosition;
		public Boolean userOrNot = false;
		GameObject _directionsGO;
		private bool _recalculateNext;
		public string _routeType = "Walking";
		public Text duration;
		public int routeNum = 0;
		public Button button2;
		protected virtual void Awake()
		{
			if (_map == null)
			{
				_map = FindObjectOfType<AbstractMap>();
			}
			_directions = MapboxAccess.Instance.Directions;
			_map.OnInitialized += Query;
			_map.OnUpdated += Query;
			duration = GameObject.Find("Duration").GetComponent<Text>();
		}

		public void Start()
		{
			_cachedWaypoints = new List<Vector3>();
			foreach (var item in _waypoints)
			{
				if(userOrNot && _wayCounter == 0)
                {
					Vector3 b = _userPosition;
					b.y = -13;
					item.position = b;
					_cachedWaypoints.Add(item.position);
                }
                else
                {
					Vector3 c = Conversions.GeoToWorldPosition(_waypointsGeo[_wayCounter].x, _waypointsGeo[_wayCounter].y, _map.CenterMercator, _map.WorldRelativeScale).ToVector3xz();
					c.y = -13;
					item.position = c;
					_cachedWaypoints.Add(item.position);
				}
				_wayCounter++;
			}
			_recalculateNext = false;

			foreach (var modifier in MeshModifiers)
			{
				modifier.Initialize();
			}
			Query();
			StartCoroutine(QueryTimer());
		}

		protected virtual void OnDestroy()
		{
			_map.OnInitialized -= Query;
			_map.OnUpdated -= Query;
		}

		void Query()
		{
			var count = _waypoints.Length;
			var wp = new Vector2d[count];

			for (int i = 0; i < count; i++)
			{
				wp[i] = _waypoints[i].GetGeoPosition(_map.CenterMercator, _map.WorldRelativeScale);
			}
			if(_routeType == "Walking")
            {
				var _directionResource = new DirectionResource(wp, RoutingProfile.Cycling);
				_directionResource.Alternatives = true;
				_directionResource.Steps = true;
				_directions.Query(_directionResource, HandleDirectionsResponse);
			}
			else
			{
				var _directionResource = new DirectionResource(wp, RoutingProfile.Driving);
				_directionResource.Alternatives = true;
				_directionResource.Steps = true;
				_directions.Query(_directionResource, HandleDirectionsResponse);
			}
		}

		public IEnumerator QueryTimer()
		{
			while (true)
			{
				yield return new WaitForSeconds(UpdateFrequency);
				for (int i = 0; i < _waypoints.Length; i++)
				{
					if (_waypoints[i].position != _cachedWaypoints[i])
					{
						_recalculateNext = true;
						_cachedWaypoints[i] = _waypoints[i].position;
					}
				}

				if (_recalculateNext)
				{
					Query();
					_recalculateNext = false;
				}
			}
		}

		void HandleDirectionsResponse(DirectionsResponse response)
		{
			button2 = GameObject.Find("Route2").GetComponent<Button>();
			if (response.Routes.Count() == 1)
            {
				button2.interactable = false;
			}
            else
            {
				button2.interactable = true;
            }
			

			if (response == null || null == response.Routes || response.Routes.Count < 1)
			{
				return;
			}

			var meshData = new MeshData();
			var dat = new List<Vector3>();
			_waypointsOnMap = new List<Vector3>();
			_arMark.Clear();
			if(routeNum == 1)
            {
				if(response.Routes.Count == 1)
                {
					foreach (var point in response.Routes[0].Geometry)
					{
						_arMark.Add(new Vector2d(point.x, point.y));
						dat.Add(Conversions.GeoToWorldPosition(point.x, point.y, _map.CenterMercator, _map.WorldRelativeScale).ToVector3xz());
					}
					int timet = (int)response.Routes[0].Duration / 60;
					double times = (int)response.Routes[0].Duration % 60;
					duration.text = "time: " + timet.ToString() + " min " + times.ToString() + " sec ";
				}
                else
                {
					foreach (var point in response.Routes[1].Geometry)
					{
						_arMark.Add(new Vector2d(point.x, point.y));
						dat.Add(Conversions.GeoToWorldPosition(point.x, point.y, _map.CenterMercator, _map.WorldRelativeScale).ToVector3xz());
					}
					int timet = (int)response.Routes[1].Duration / 60;
					double times = (int)response.Routes[1].Duration % 60;
					duration.text = "time: " + timet.ToString() + " min " + times.ToString() + " sec ";
				}
			}
            else
            {
				foreach (var point in response.Routes[0].Geometry)
				{
					_arMark.Add(new Vector2d(point.x, point.y));
					dat.Add(Conversions.GeoToWorldPosition(point.x, point.y, _map.CenterMercator, _map.WorldRelativeScale).ToVector3xz());
				}
				int timet = (int)response.Routes[0].Duration / 60;
				double times = (int)response.Routes[0].Duration % 60;
				duration.text = "Duration time: " + timet.ToString() + " min " + times.ToString() + " sec ";
			}
			
			var feat = new VectorFeatureUnity();
			feat.Points.Add(dat);
			_waypointsOnMap = dat;

			foreach (MeshModifier mod in MeshModifiers.Where(x => x.Active))
			{
				mod.Run(feat, meshData, _map.WorldRelativeScale);
			}

			CreateGameObject(meshData);
		}

		GameObject CreateGameObject(MeshData data)
		{
			
			if (_directionsGO != null)
			{
				_directionsGO.Destroy();
			}
			_directionsGO = new GameObject("direction waypoint " + " entity");
			var mesh = _directionsGO.AddComponent<MeshFilter>().mesh;
			mesh.subMeshCount = data.Triangles.Count;

			mesh.SetVertices(data.Vertices);
			_counter = data.Triangles.Count;
			for (int i = 0; i < _counter; i++)
			{
				var triangle = data.Triangles[i];
				mesh.SetTriangles(triangle, i);
			}

			_counter = data.UV.Count;
			for (int i = 0; i < _counter; i++)
			{
				var uv = data.UV[i];
				mesh.SetUVs(i, uv);
			}
			mesh.RecalculateNormals();
			_directionsGO.AddComponent<MeshRenderer>().material = _material;
			return _directionsGO;
		}

		public void Refresh()
		{
			Query();
		}

		public Vector2d TransferName(Transform transform)
        {
			var v = transform.GetGeoPosition(_map.CenterMercator, _map.WorldRelativeScale);
			return v;
        }
	}

}
