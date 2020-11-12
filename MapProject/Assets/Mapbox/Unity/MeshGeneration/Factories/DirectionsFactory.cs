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

		[SerializeField]
		[Range(1, 10)]
		private float UpdateFrequency = 2;

		public Vector2d[] _waypointsGeo;
		public List<Vector2d> _arMark;
		private int _wayCounter = 0;
		private Directions _directions;
		private int _counter;

		public Vector3 _userPosition;
		public Boolean userOrNot = false;
		GameObject _directionsGO;
		private bool _recalculateNext;
		public string _routeType = "Walking";
		protected virtual void Awake()
		{
			if (_map == null)
			{
				_map = FindObjectOfType<AbstractMap>();
			}
			_directions = MapboxAccess.Instance.Directions;
			_map.OnInitialized += Query;
			_map.OnUpdated += Query;
		}

		public void Start()
		{
			_cachedWaypoints = new List<Vector3>();
			foreach (var item in _waypoints)
			{
				if(userOrNot && _wayCounter == 0)
                {
					item.position = _userPosition;
					_cachedWaypoints.Add(item.position);
                }
                else
                {
					Vector3 c = Conversions.GeoToWorldPosition(_waypointsGeo[_wayCounter].x, _waypointsGeo[_wayCounter].y, _map.CenterMercator, _map.WorldRelativeScale).ToVector3xz();
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
				var _directionResource = new DirectionResource(wp, RoutingProfile.Walking);
				_directionResource.Steps = true;
				_directions.Query(_directionResource, HandleDirectionsResponse);
			}
			else
			{
				var _directionResource = new DirectionResource(wp, RoutingProfile.Driving);
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
			if (response == null || null == response.Routes || response.Routes.Count < 1)
			{
				return;
			}

			var meshData = new MeshData();
			var dat = new List<Vector3>();
			_arMark.Clear();
			foreach (var point in response.Routes[0].Geometry)
			{
				_arMark.Add(new Vector2d(point.x, point.y));
				dat.Add(Conversions.GeoToWorldPosition(point.x, point.y, _map.CenterMercator, _map.WorldRelativeScale).ToVector3xz());
			}
			
			var feat = new VectorFeatureUnity();
			feat.Points.Add(dat);

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
			_cachedWaypoints.Add(_waypoints.Last().position);
			Query();
		}

		public Vector2d TransferName(Transform transform)
        {
			var v = transform.GetGeoPosition(_map.CenterMercator, _map.WorldRelativeScale);
			return v;
        }
	}

}
