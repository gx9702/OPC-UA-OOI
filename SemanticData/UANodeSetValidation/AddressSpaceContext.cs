﻿//___________________________________________________________________________________
//
//  Copyright (C) 2019, Mariusz Postol LODZ POLAND.
//
//  To be in touch join the community at GITTER: https://gitter.im/mpostol/OPC-UA-OOI
//___________________________________________________________________________________

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using UAOOI.SemanticData.BuildingErrorsHandling;
using UAOOI.SemanticData.InformationModelFactory;
using UAOOI.SemanticData.UANodeSetValidation.DataSerialization;
using UAOOI.SemanticData.UANodeSetValidation.InformationModelFactory;
using UAOOI.SemanticData.UANodeSetValidation.UAInformationModel;
using UAOOI.SemanticData.UANodeSetValidation.Utilities;
using UAOOI.SemanticData.UANodeSetValidation.XML;

namespace UAOOI.SemanticData.UANodeSetValidation
{

  /// <summary>
  /// Class AddressSpaceContext - responsible to manage all nodes in the OPC UA Address Space.
  /// </summary>
  internal class AddressSpaceContext : IAddressSpaceContext, IAddressSpaceBuildContext, IAddressSpaceValidationContext
  {

    #region constructor
    /// <summary>
    /// Initializes a new instance of the <see cref="AddressSpaceContext" /> class.
    /// </summary>
    /// <param name="traceEvent">Encapsulates an action to trace the progress and validation issues.</param>
    /// <exception cref="ArgumentNullException">traceEvent - traceEvent - cannot be null</exception>
    public AddressSpaceContext(Action<TraceMessage> traceEvent)
    {
      m_TraceEvent = traceEvent ?? throw new ArgumentNullException("traceEvent", "traceEvent - cannot be null");
      m_Validator = new Validator(this);
      m_NamespaceTable = new NamespaceTable();
      m_TraceEvent(TraceMessage.DiagnosticTraceMessage("Entering AddressSpaceContext creator - starting creation the OPC UA Address Space."));
      UANodeSet _standard = UANodeSet.ReadUADefinedTypes();
      m_TraceEvent(TraceMessage.DiagnosticTraceMessage("Address Space - the OPC UA defined has been uploaded."));
      ImportNodeSet(_standard);
      m_TraceEvent(TraceMessage.DiagnosticTraceMessage("Address Space - has bee created successfully."));
    }
    #endregion

    #region IAddressSpaceContext
    /// <summary>
    /// Sets the information model factory, which can be used to export a part of the OPC UA Address Space. If not set or set null an internal stub implementation will be used.
    /// </summary>
    /// <value>The information model factory.</value>
    /// <remarks>It is defined to handle dependency injection.</remarks>
    public IModelFactory InformationModelFactory
    {
      set
      {
        if (value == null)
          m_InformationModelFactory = new InformationModelFactoryBase();
        else
          m_InformationModelFactory = value;
      }
      private get => m_InformationModelFactory;
    }
    /// <summary>
    /// Imports a part of the OPC UA Address Space contained in the <see cref="UANodeSet" /> object model.
    /// </summary>
    /// <param name="model">The model to be imported.</param>
    /// <exception cref="System.ArgumentNullException">model;the model cannot be null</exception>
    void IAddressSpaceContext.ImportUANodeSet(UANodeSet model)
    {
      m_TraceEvent(TraceMessage.DiagnosticTraceMessage("Entering AddressSpaceContextService.ImportUANodeSet - importing from object model."));
      if (model == null)
        throw new ArgumentNullException("model", "the model cannot be null");
      ImportNodeSet(model);
    }
    /// <summary>
    /// Imports a part of the OPC UA Address Space contained in the file <see cref="FileInfo" />.
    /// </summary>
    /// <param name="model">The model to be imported.</param>
    /// <exception cref="System.IO.FileNotFoundException">The imported file does not exist</exception>
    void IAddressSpaceContext.ImportUANodeSet(FileInfo model)
    {
      m_TraceEvent(TraceMessage.DiagnosticTraceMessage("Entering AddressSpaceContextService.ImportUANodeSet - importing form file"));
      if (model == null)
        throw new ArgumentNullException("model", "the model cannot be null");
      if (!model.Exists)
        throw new FileNotFoundException("The imported file does not exist", model.FullName);
      UANodeSet _nodeSet = UANodeSet.ReadModelFile(model);
      ImportNodeSet(_nodeSet);
    }
    /// <summary>
    /// Validates and exports the selected model for the default namespace at index 1 if defined or standard OPC UA.
    /// </summary>
    void IAddressSpaceContext.ValidateAndExportModel()
    {
      ushort _nsi = m_NamespaceTable.LastNamespaceIndex;
      string _namespace = m_NamespaceTable.GetString(_nsi);
      m_TraceEvent(TraceMessage.DiagnosticTraceMessage(string.Format("Entering AddressSpaceContext.ValidateAndExportModel - starting for the {0} namespace.", _namespace)));
      ValidateAndExportModel(_nsi, m_Validator);
    }
    /// <summary>
    /// Validates and exports the selected model.
    /// </summary>
    /// <param name="targetNamespace">The target namespace of the validated model.</param>
    /// <exception cref="System.ArgumentOutOfRangeException">targetNamespace;Cannot find this namespace</exception>
    void IAddressSpaceContext.ValidateAndExportModel(string targetNamespace)
    {
      m_TraceEvent(TraceMessage.DiagnosticTraceMessage(string.Format("Entering IAddressSpaceContext.ValidateAndExportModel - starting for the {0} namespace.", targetNamespace)));
      int _nsIndex = m_NamespaceTable.GetIndex(targetNamespace);
      if (_nsIndex == -1)
        throw new ArgumentOutOfRangeException("targetNamespace", "Cannot find this namespace");
      ValidateAndExportModel(_nsIndex, m_Validator);
    }
    #endregion

    #region IAddressSpaceBuildContext
    /// <summary>
    /// Search the address space to find the node <paramref name="nodeId" /> and returns <see cref="XmlQualifiedName" />
    /// encapsulating the <see cref="UANode.BrowseName" /> of this node if exist. Returns<c>null</c> otherwise.
    /// </summary>
    /// <param name="nodeId">The identifier of the node to find.</param>
    /// <param name="defaultValue">The default value.</param>
    /// <returns>An instance of <see cref="XmlQualifiedName" /> representing the <see cref="UANode.BrowseName" /> of the node indexed by <paramref name="nodeId" /></returns>
    public XmlQualifiedName ExportBrowseName(NodeId nodeId, NodeId defaultValue)
    {
      if (nodeId == defaultValue)
        return null;
      IUANodeContext _context = TryGetUANodeContext(nodeId, m_TraceEvent);
      if (_context == null)
        return null;
      return _context.ExportNodeBrowseName();
    }
    /// <summary>
    /// Exports the argument for a method.
    /// </summary>
    /// <param name="argument">The argument - it defines a Method input or output argument specification. It is for example used in the input and output argument Properties for Methods.</param>
    /// <param name="dataType">Type of the data.</param>
    /// <returns>Parameter.</returns>
    public Parameter ExportArgument(DataSerialization.Argument argument, XmlQualifiedName dataType)
    {
      Parameter _ret = new Parameter()
      {
        ArrayDimensions = argument.ArrayDimensions.ArrayDimensionsToString(),
        DataType = dataType,
        Identifier = new Nullable<int>(),
        Name = argument.Name,
        ValueRank = argument.ValueRank.GetValueRank(m_TraceEvent)
      };
      if (argument.Description != null)
        _ret.AddDescription(argument.Description.Locale, argument.Description.Text);
      return _ret;
    }
    /// <summary>
    /// Gets the or create node context.
    /// </summary>
    /// <param name="nodeId">The node identifier.</param>
    /// <param name="createUAModelContext">Delegated capturing functionality to create ua model context.</param>
    /// <returns>Returns an instance of <see cref="IUANodeContext" />.</returns>
    public IUANodeContext GetOrCreateNodeContext(NodeId nodeId, Func<NodeId, IUANodeContext> createUAModelContext)
    {
      string _idKey = nodeId.ToString();
      if (!m_NodesDictionary.TryGetValue(_idKey, out IUANodeContext _ret))
      {
        _ret = createUAModelContext(nodeId);
        m_NodesDictionary.Add(_idKey, _ret);
      }
      return _ret;
    }
    /// <summary>
    /// Gets the index or append.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>System.UInt16.</returns>
    public ushort GetIndexOrAppend(string value)
    {
      return m_NamespaceTable.GetIndexOrAppend(value);
    }
    /// <summary>
    /// Gets the namespace.
    /// </summary>
    /// <param name="namespaceIndex">Index of the namespace.</param>
    public string GetNamespace(ushort namespaceIndex)
    {
      return m_NamespaceTable.GetString(namespaceIndex);
    }
    /// <summary>
    /// Gets my references from the main collection.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <returns>An instance of the <see cref="IEnumerable{UAReferenceContext}"/> containing references pointed out by index.</returns>
    IEnumerable<UAReferenceContext> IAddressSpaceBuildContext.GetMyReferences(IUANodeBase index)
    {
      return m_References.Values.Where<UAReferenceContext>(x => (x.ParentNode == index));
    }
    /// <summary>
    /// Gets the references2 me.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <returns>IEnumerable&lt;UAReferenceContext&gt;.</returns>
    IEnumerable<UAReferenceContext> IAddressSpaceBuildContext.GetReferences2Me(IUANodeContext index)
    {
      return m_References.Values.Where<UAReferenceContext>(x => x.TargetNode == index && x.ParentNode != index);
    }
    /// <summary>
    /// Gets the children nodes for the <paramref name="rootNode" />.
    /// </summary>
    /// <param name="rootNode">The root node of the requested children.</param>
    /// <returns>Return an instance of <see cref="IEnumerable" /> capturing all children of the selected node.</returns>
    public IEnumerable<IUANodeBase> GetChildren(IUANodeContext rootNode)
    {
      return m_References.Values.Where<UAReferenceContext>(x => x.SourceNode == rootNode).
                                                                  Where<UAReferenceContext>(x => (x.ReferenceKind == ReferenceKindEnum.HasProperty || x.ReferenceKind == ReferenceKindEnum.HasComponent)).
                                                                  Select<UAReferenceContext, IUANodeContext>(x => x.TargetNode);
    }
    public Parameter ExportArgument(DataSerialization.Argument argument)
    {
      XmlQualifiedName _dataType = ExportBrowseName(NodeId.Parse(argument.DataType.Identifier), DataTypeIds.BaseDataType);
      return ExportArgument(argument, _dataType);
    }

    //TODO #40 remove commented functionality
    ///// <summary>
    ///// Gets an instance of the <see cref="IAddressSpaceBuildContext"/> representing selected by <paramref name="nodeClass"/> base type node if applicable, null otherwise.
    ///// </summary>
    ///// <param name="nodeClass">The node class selector.</param>
    ///// <returns>An  instance of <see cref="IUANodeBase"/> representing base type for selected node class.</returns>
    ///// <exception cref="ApplicationException"> If <paramref name="nodeClass"/> is equal <see cref="NodeClassEnum.Unknown"/></exception>
    //IUANodeBase IAddressSpaceBuildContext.GetBaseTypeNode(NodeClassEnum nodeClass)
    //{
    //  IUANodeContext _ret = null;
    //  switch (nodeClass)
    //  {
    //    case NodeClassEnum.UADataType:
    //      m_NodesDictionary.TryGetValue(DataTypeIds.BaseDataType.ToString(), out _ret);
    //      break;
    //    case NodeClassEnum.UAMethod:
    //      break;
    //    case NodeClassEnum.UAObjectType:
    //    case NodeClassEnum.UAObject:
    //      m_NodesDictionary.TryGetValue(ObjectTypeIds.BaseObjectType.ToString(), out _ret);
    //      break;
    //    case NodeClassEnum.UAReferenceType:
    //      m_NodesDictionary.TryGetValue(ReferenceTypeIds.References.ToString(), out _ret);
    //      break;
    //    case NodeClassEnum.UAVariable:
    //    case NodeClassEnum.UAVariableType:
    //      m_NodesDictionary.TryGetValue(VariableTypeIds.BaseVariableType.ToString(), out _ret);
    //      break;
    //    case NodeClassEnum.UAView:
    //      break;
    //    case NodeClassEnum.Unknown:
    //      throw new ApplicationException($"In {nameof(IAddressSpaceBuildContext.GetBaseTypeNode)} the {nameof(NodeClass)} must not be {nameof(NodeClassEnum.Unknown)}");
    //  }
    //  return _ret;
    //}
    #endregion    

    #region IAddressSpaceValidationContext
    /// <summary>
    /// Exports the current namespace table containing all namespaces that have been registered.
    /// </summary>
    /// <value>An instance of <see cref="IEnumerable{IModelTableEntry}" /> containing.</value>
    public IEnumerable<IModelTableEntry> ExportNamespaceTable => m_NamespaceTable.ExportNamespaceTable;
    #endregion

    #region private
    //vars
    private readonly IValidator m_Validator;
    private IModelFactory m_InformationModelFactory = new InformationModelFactoryBase();
    private Dictionary<string, UAReferenceContext> m_References = new Dictionary<string, UAReferenceContext>();
    private NamespaceTable m_NamespaceTable = null;
    private Dictionary<string, IUANodeContext> m_NodesDictionary = new Dictionary<string, IUANodeContext>();
    private readonly Action<TraceMessage> m_TraceEvent = BuildErrorsHandling.Log.TraceEvent;
    //methods
    private void ImportNodeSet(UANodeSet model)
    {
      if (model.ServerUris != null)
        m_TraceEvent(TraceMessage.BuildErrorTraceMessage(BuildError.NotSupportedFeature, "ServerUris is omitted during the import"));
      if (model.Extensions != null)
        m_TraceEvent(TraceMessage.BuildErrorTraceMessage(BuildError.NotSupportedFeature, "Extensions is omitted during the import"));
      string _namespace = model.NamespaceUris == null ? m_NamespaceTable.GetString(0) : model.NamespaceUris[0];
      m_TraceEvent(TraceMessage.DiagnosticTraceMessage(string.Format("Entering AddressSpaceContext.ImportNodeSet - starting import {0}.", _namespace)));
      IUAModelContext _modelContext = new UAModelContext(model, this);
      model.RecalculateNodeIds(_modelContext);
      m_TraceEvent(TraceMessage.DiagnosticTraceMessage("AddressSpaceContext.ImportNodeSet - context for imported model is created and starting import nodes."));
      foreach (UANode _nd in model.Items)
        this.ImportUANode(_nd, m_TraceEvent);
      m_TraceEvent(TraceMessage.DiagnosticTraceMessage(string.Format("Finishing AddressSpaceContext.ImportNodeSet - imported {0} nodes.", model.Items.Length)));
    }
    private void ImportUANode(UANode node, Action<TraceMessage> traceEvent)
    {
      try
      {
        NodeId _nodeId = NodeId.Parse(node.NodeId);
        IUANodeContext _newNode = GetOrCreateNodeContext(_nodeId, x => new UANodeContext(_nodeId, this));
        _newNode.Update(node, _reference =>
              {
                if (!m_References.ContainsKey(_reference.Key))
                  m_References.Add(_reference.Key, _reference);
              });
      }
      catch (Exception _ex)
      {
        string _msg = string.Format("ImportUANode {1} is interrupted by exception {0}", _ex.Message, node.NodeId);
        m_TraceEvent(TraceMessage.DiagnosticTraceMessage(_msg));
      }
    }
    private IUANodeContext TryGetUANodeContext(NodeId nodeId, Action<TraceMessage> traceEvent)
    {
      if (!m_NodesDictionary.TryGetValue(nodeId.ToString(), out IUANodeContext _ret))
      {
        traceEvent(TraceMessage.BuildErrorTraceMessage(BuildError.NodeIdNotDefined, string.Format("References to node with NodeId: {0} is omitted during the import.", nodeId)));
        return null;
      }
      if (_ret.UANode == null)
      {
        traceEvent(TraceMessage.BuildErrorTraceMessage(BuildError.NodeIdNotDefined, string.Format("NodeId: {0} is omitted during the import.", nodeId)));
        return null;
      }
      return _ret;
    }
    private void GetBaseTypes(IUANodeContext rootNode, List<IUANodeContext> inheritanceChain)
    {
      if (rootNode == null)
        throw new ArgumentNullException("rootNode");
      if (rootNode.InRecursionChain)
        throw new ArgumentOutOfRangeException("Circular reference");
      rootNode.InRecursionChain = true;
      IEnumerable<IUANodeContext> _derived = m_References.Values.Where<UAReferenceContext>(x => (x.TypeNode.NodeIdContext == ReferenceTypeIds.HasSubtype) && (x.TargetNode == rootNode)).
                                                                Select<UAReferenceContext, IUANodeContext>(x => x.SourceNode);
      inheritanceChain.AddRange(_derived);
      if (_derived.Count<IUANodeContext>() > 1)
        throw new ArgumentOutOfRangeException("To many subtypes");
      else if (_derived.Count<IUANodeContext>() == 1)
        GetBaseTypes(_derived.First<IUANodeContext>(), inheritanceChain);
      rootNode.InRecursionChain = false;
    }
    private void ValidateAndExportModel(int nameSpaceIndex, IValidator validator)
    {
      IEnumerable<IUANodeContext> _stubs = from _key in m_NodesDictionary.Values where _key.NodeIdContext.NamespaceIndex == nameSpaceIndex select _key;
      List<IUANodeContext> _nodes = (from _node in _stubs where _node.UANode != null && (_node.UANode is UAType) select _node).ToList();
      IUANodeBase _objects = TryGetUANodeContext(UAInformationModel.ObjectIds.ObjectsFolder, m_TraceEvent);
      if (_objects is null)
        throw new ArgumentNullException("Cannot find ObjectsFolder in the standard information model");
      IEnumerable<IUANodeContext> _allInstances = m_References.Values.Where<UAReferenceContext>(x => (x.SourceNode.NodeIdContext == ObjectIds.ObjectsFolder) &&
                                                                                                     (x.TypeNode.NodeIdContext == ReferenceTypeIds.Organizes) &&
                                                                                                     (x.TargetNode.NodeIdContext.NamespaceIndex == nameSpaceIndex))
                                                                                                     .Select<UAReferenceContext, IUANodeContext>(x => x.TargetNode);
      _nodes.AddRange(_allInstances);
      m_TraceEvent(TraceMessage.DiagnosticTraceMessage(string.Format("AddressSpaceContext.ValidateAndExportModel - selected {0} nodes to be added to the model.", _nodes.Count)));
      List<BuildError> _errors = new List<BuildError>(); //TODO should be added to the model;
      foreach (IModelTableEntry _ns in ExportNamespaceTable)
      {
        string _publicationDate = _ns.PublicationDate.HasValue ? _ns.PublicationDate.Value.ToShortDateString() : DateTime.UtcNow.ToShortDateString();
        string _version = _ns.Version;
        InformationModelFactory.CreateNamespace(_ns.ModelUri, _publicationDate, _version);
      }
      string _msg = null;
      int _nc = 0;
      foreach (IUANodeBase _item in _nodes)
      {
        try
        {
          validator.ValidateExportNode(_item, InformationModelFactory, null); //y =>
          _nc++;
        }
        catch (Exception _ex)
        {
          _msg = string.Format("Error caught while processing the node {0}. The message: {1} at {2}.", _item.UANode.NodeId, _ex.Message, _ex.StackTrace);
          m_TraceEvent(TraceMessage.BuildErrorTraceMessage(BuildError.NonCategorized, _msg));
        }
      }
      if (_errors.Count == 0)
        _msg = string.Format("Finishing Validator.ValidateExportModel - the model contains {0} nodes.", _nc);
      else
        _msg = string.Format("Finishing Validator.ValidateExportModel - the model contains {0} nodes and {1} errors.", _nc, _errors.Count);
      m_TraceEvent(TraceMessage.DiagnosticTraceMessage(_msg));
    }
    #endregion

    #region UnitTestd
    [System.Diagnostics.Conditional("DEBUG")]
    internal void UTAddressSpaceCheckConsistency(Action<IUANodeContext> returnValue)
    {
      foreach (IUANodeContext _node in m_NodesDictionary.Values.Where<IUANodeBase>(x => x.UANode is null))
        returnValue(_node);
    }
    [System.Diagnostics.Conditional("DEBUG")]
    internal void UTReferencesCheckConsistency(Action<IUANodeContext, IUANodeContext, IUANodeContext, IUANodeContext> returnValue)
    {
      foreach (UAReferenceContext _node in m_References.Values)
        if (_node.SourceNode is null || _node.ParentNode is null || _node.TargetNode is null || _node.TypeNode is null)
          returnValue(_node?.SourceNode, _node?.ParentNode, _node?.TargetNode, _node?.TargetNode);
    }
    [System.Diagnostics.Conditional("DEBUG")]
    internal void UTTryGetUANodeContext(NodeId nodeId, Action<IUANodeContext> returnValue)
    {
      returnValue(TryGetUANodeContext(nodeId, x => { }));
    }
    [System.Diagnostics.Conditional("DEBUG")]
    internal void UTGetReferences(NodeId source, Action<UAReferenceContext> returnValue)
    {
      foreach (UAReferenceContext _ref in m_References.Values.Where<UAReferenceContext>(x => (x.SourceNode.NodeIdContext == source)))
        returnValue(_ref);
    }
    [System.Diagnostics.Conditional("DEBUG")]
    internal void UTValidateAndExportModel(int nameSpaceIndex, Action<IEnumerable<IUANodeContext>> returnValue)
    {
      returnValue((from _key in m_NodesDictionary.Values where _key.NodeIdContext.NamespaceIndex == nameSpaceIndex select _key));
    }
    #endregion

  }

}
