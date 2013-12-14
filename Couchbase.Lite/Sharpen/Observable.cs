using System;

namespace Sharpen
{
  public class Observer 
  {
    void Update(Observable o, Object arg) {
      throw new NotImplementedException();

    }
  }

  public class Observable
  {
      public void AddObserver(Observer o){
        throw new NotImplementedException();
      }

      protected void ClearChanged() {
        throw new NotImplementedException();
      }

      public int CountObservers()  {
        throw new NotImplementedException();
      }

      public void DeleteObserver(Observer o) {

      }

      public void DeleteObservers() {
        throw new NotImplementedException();
      }

      public Boolean HasChanged() {
        throw new NotImplementedException();       
      }

      public void NotifyObservers() {
        throw new NotImplementedException();
      }

      public void NotifyObservers(Object arg) {
        throw new NotImplementedException();
      }

      protected void SetChanged() { 
        throw new NotImplementedException();
      }
  }
}

