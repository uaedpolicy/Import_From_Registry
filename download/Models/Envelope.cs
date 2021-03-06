﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

namespace Download.Models
{
	public class Envelope
	{
		[JsonProperty( PropertyName = "envelope_type" )]
		public string EnvelopeType { get; set; }

		[JsonProperty( PropertyName = "envelope_version" )]
		public string EnvelopeVersion { get; set; }

		[JsonProperty( PropertyName = "envelope_ceterms_ctid" )]
		public string EnvelopeCetermsCtid { get; set; }

		[JsonProperty( PropertyName = "envelope_ctdl_type" )]
		public string EnvelopeCtdlType { get; set; }

		/// <summary>
		/// Not used for an add - may be added back later
		/// </summary>
		//[JsonProperty( PropertyName = "envelope_id" )]
		//public string EnvelopeIdentifier { get; set; }

		[JsonProperty( PropertyName = "envelope_community" )]
		public string EnvelopeCommunity { get; set; }

		[JsonProperty( PropertyName = "resource" )]
		public string Resource { get; set; }

		[JsonProperty( PropertyName = "resource_format" )]
		public string ResourceFormat { get; set; }

		[JsonProperty( PropertyName = "resource_encoding" )]
		public string ResourceEncoding { get; set; }

		[JsonProperty( PropertyName = "resource_public_key" )]
		public string ResourcePublicKey { get; set; }
	}



	public class ReadEnvelope : Envelope
	{
		[JsonProperty( PropertyName = "envelope_id" )]
		public string EnvelopeIdentifier { get; set; }


		[JsonProperty( PropertyName = "decoded_resource" )]
		public object DecodedResource { get; set; }

		//probably don't care about the headers, but include for now
		[JsonProperty( PropertyName = "node_headers" )]
		public NodeHeader NodeHeaders { get; set; }

		[JsonProperty( PropertyName = "publisher_id" )]
		public string PublisherId { get; set; }

		[JsonProperty( PropertyName = "secondary_publisher_id" )]
		public string SecondaryPublisherId { get; set; }
	}
	public class NodeHeader
	{
		[JsonProperty( PropertyName = "resource_digest" )]
		public string ResourceDigest { get; set; }

		[JsonProperty( PropertyName = "revision_history" )]
		public List<NodeVersion> NodeVersions { get; set; }

		[JsonProperty( PropertyName = "created_at" )]
		public string CreatedAt { get; set; }

		[JsonProperty( PropertyName = "updated_at" )]
		public string UpdatedAt { get; set; }

		[JsonProperty( PropertyName = "deleted_at" )]
		public string DeletedAt { get; set; }
	}

	public class NodeVersion
	{
		[JsonProperty( PropertyName = "head" )]
		public string head { get; set; }

		[JsonProperty( PropertyName = "event" )]
		public string EventType { get; set; }

		[JsonProperty( PropertyName = "created_at" )]
		public string CreatedAt { get; set; }

		[JsonProperty( PropertyName = "actor" )]
		public string Actor { get; set; }

		[JsonProperty( PropertyName = "url" )]
		public string Url { get; set; }

	}

}
