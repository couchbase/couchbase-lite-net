
function highlightRanges(rawRanges) {
  if (!rawRanges.length)
    return;
  
  var ranges = new Array();
  for (var i = 0; i < rawRanges.length; i++)
    ranges[i] = convertRange(rawRanges[i]);
  ranges.sort(compareRanges);

  var content = document.getElementById("content");
  var source = content.innerHTML;
  var result = "";
  
  var position = 0;
  var previousPosition = 0;
  var line = 1;
  var column = 1;
  
  var currentRangeIndex = 0;
  var currentRange = ranges[currentRangeIndex];
  var previousCoverageStatus = null;

  var escapeSequenceRegularExpression = /^(&amp;|&lt;|&gt;|&quot;)/;
  var maxEscapeSequenceLength = getMaxStringLength(escapeSequenceRegularExpression.source.match(new RegExp(escapeSequenceRegularExpression.source.substr(1), "g")));

  while (position < source.length) {
    // testing for exact beginning of the next range
    if (line == currentRange.line && column == currentRange.column) {
      if (previousCoverageStatus == null) {
        previousCoverageStatus = currentRange.covered;
        result = result.concat(source.substring(previousPosition, position), "<a class='", (currentRange.covered ? "covered" : "uncovered"), "' id='s", currentRange.line, ".", currentRange.column, "'>");
        previousPosition = position;
      } else {
        throw "invalid state";
      }
    }
    // testing for the end of the range, but taking into account possible wrong range that goes beyound end of line
    else if (line > currentRange.endLine || line == currentRange.endLine && column >= currentRange.endColumn) {
      if (previousCoverageStatus != null) {
        result = result.concat(source.substring(previousPosition, position), "</a>");
        previousPosition = position;
        previousCoverageStatus = null;
        do {
          do {
            currentRangeIndex++;
            currentRange = currentRangeIndex < ranges.length ? ranges[currentRangeIndex] : null;
          } // skipping duplicates
          while (currentRange != null && compareRanges(currentRange, ranges[currentRangeIndex - 1]) == 0);
        } // skipping (overlapping) ranges that already passed
        while (currentRange != null && (line > currentRange.endLine || line == currentRange.endLine && column >= currentRange.endColumn));
        if (currentRange == null) {
          break;
        }
        // adjusting (overlapping) ranges that not yet passed,  so they are started at current pos
        if (line > currentRange.line || line == currentRange.line && column >= currentRange.column) {
          currentRange.line = line;
          currentRange.column = column;
          continue;
        }
      } else {
        throw "invalid state";
      }
    }
    if (source.charAt(position) == '\r') {
      position++;
    }
    if (source.charAt(position) == '\n') {
      position++;
      line++;
      column = 1;
    } else {
      // shift according to found escape sequence
      var matches = source.substr(position, maxEscapeSequenceLength).match(escapeSequenceRegularExpression);
      if (matches != null && matches.length > 0)
        position += matches[0].length;
      else
        position++;

      column++;
    }
  }
  result = result.concat(source.substring(previousPosition), (previousCoverageStatus == null) ? "" : "</a>");
  content.outerHTML = "<pre id='" + content.id + "' class='" + content.className + "'>" + result + "</pre>";
}

function convertRange(rawRange) {
  var range = {};
  range.line = rawRange[0];
  range.column = rawRange[1];
  range.endLine = rawRange[2];
  range.endColumn = rawRange[3];
  range.covered = rawRange[4];
  return range;
}

function compareRanges(a, b) {
  var result;
  if (result = compareValues(a.line, b.line))
    return result;
  if (result = compareValues(a.column, b.column))
    return result;
  if (result = compareValues(a.endLine, b.endLine))
    return result;
  if (result = compareValues(a.endColumn, b.endColumn))
    return result;
  return 0;
}

function compareValues(a, b) {
  if (a == b)
    return 0;
  return (a < b) ? -1 : 1;
}

function getMaxStringLength(stringArray) {
  var maxLength = 0;
  for (var i = 0; i < stringArray.length; ++i)
    if (stringArray[i].length > maxLength)
      maxLength = stringArray[i].length;
  return maxLength;
}

