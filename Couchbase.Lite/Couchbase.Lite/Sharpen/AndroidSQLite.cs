using System;
using System.Collections;

namespace Couchbase.Lite
{
  public class AndroidSQLite
  {
    public Cursor RawQuery(string query) {
      throw new NotImplementedException();
    }

    public void ExecSQL(string statement) {
      throw new NotImplementedException();
    }

    public int Insert(string statement) {
      throw new NotImplementedException();
    }

    public int Update(string statement, object values, string whereClause, string[] whereArgs) {
      throw new NotImplementedException();
    }
  }
}