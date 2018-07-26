$(document).ready(function() {
  resizeContent();
  $(window).bind("resize", resizeContent);
  $("#content").splitter({
    type: "v",
    outline: true,
    minLeft: 200,
    resizeToWidth: true,
    cookie: "splitter"
  });
  $("#coverageTree").dynatree({
    minExpandLevel: 2,
    onPostInit: initializeTree,
    onLazyRead: loadChildren,
    onCustomRender: createNodePresentation,
    onActivate: navigateToNode
  });
});

function initializeTree() {
  var rootNode = $("#coverageTree").dynatree("getRoot");
  rootNode.data.firstChildIndex = 0;
  loadChildren(rootNode);

  var realRootNode = rootNode.childList[0];
  loadChildren(realRootNode);
  realRootNode.focus();
}

function loadChildren(node) {
  var firstChildIndex = node.data.firstChildIndex;
  if (firstChildIndex == undefined)
    return true;

  var childrenNumber = node.data.childrenNumber;
  if (childrenNumber == undefined)
    childrenNumber = 1;

  var children = new Array();
  for (var i = 0; i < childrenNumber; i++) {
    var childIndex = firstChildIndex + i;
    var child = getNode(childIndex);
    child.key = childIndex;
    child.isLazy = child.firstChildIndex != undefined;
    child.isFolder = false;
    child.icon = false;
    children[i] = child;
  }
  node.addChild(children);
  return true;
}

function getNode(index) {
  var blockIndex = div(index, blockSize);
  var block = coverageData[blockIndex];
  if (!block)
    throw "Incorrect index";

  var itemIndex = index % blockSize;
  var data = block[itemIndex];
  if (!data)
    throw "No data for the given index";

  var node = {};
  node.name = data[0];
  node.percent = data[1];
  node.iconIndex = data[2];
  node.fileNameIndex = data[3];
  node.line = data[4];
  node.column = data[5];
  node.firstChildIndex = data[6];
  node.childrenNumber = data[7];
  return node;
}

function createNodePresentation(node) {
  var presentation = "";
  if (node.data.iconIndex >= 0)
    presentation += "<span class='i i" + node.data.iconIndex + "'>&nbsp;</span>";
  if (node.data.percent >= 0)
    presentation += "<span class='p p" + node.data.percent + "'>" + node.data.percent + "%</span>";
  var clazz = node.data.fileNameIndex ? "nws" : "n"; //node with sources or regular node
  presentation += "<a href='#' class='" + clazz + "'>" + node.data.name + "</a>";
  return presentation;
}

function navigateToNode(node) {
  var data = node.data;
  var url = resourceFolder + "/src/" + (data.fileNameIndex ? (data.fileNameIndex + ".html#s" + data.line + "." + data.column) : "nosource.html");
  $("#sourceCode").attr("src", url);
}

function resizeContent() {
  var contentHeight = ($(window).height() - 72) + "px";
  $("#content").css("height", contentHeight);
}

function div(x, y) {
  var d = x / y;
  // since js has no integer division
  if (d >= 0)
    return Math.floor(d);
  else
    return Math.ceil(d);
}